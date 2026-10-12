using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.DAL.Persistence;

// Keep historical migration sources immutable and ordinary runtime FK checks strict.
// Only seed applicability deletions need their matching child grants removed first.
public sealed class AuthorizationMigrationsSqlGenerator : IMigrationsSqlGenerator
{
    private readonly IMigrationsSqlGenerator _providerGenerator;
    private readonly MigrationsSqlGeneratorDependencies _dependencies;

    public AuthorizationMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, IServiceProvider services)
    {
        _dependencies = dependencies;
        // The pinned Oracle MySQL provider exposes this implementation as internal.
        // Construct its registered dependencies; delegate all SQL generation.
        var providerType = Assembly.Load("MySql.EntityFrameworkCore")
            .GetType("MySql.EntityFrameworkCore.Migrations.MySQLMigrationsSqlGenerator", throwOnError: true)!;
        _providerGenerator = (IMigrationsSqlGenerator)ActivatorUtilities.CreateInstance(services, providerType);
    }

    public IReadOnlyList<MigrationCommand> Generate(IReadOnlyList<MigrationOperation> operations, IModel? model = null,
        MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        var expanded = new List<MigrationOperation>();
        foreach (var operation in operations)
        {
            if (operation is DeleteDataOperation deletion && deletion.Table == "permission_account_types")
            {
                var permissionIndex = Array.IndexOf(deletion.KeyColumns, "permission_id");
                var accountIndex = Array.IndexOf(deletion.KeyColumns, "account_type");
                if (permissionIndex >= 0 && accountIndex >= 0)
                {
                    var mapping = _dependencies.TypeMappingSource.FindMapping(typeof(string))!;
                    for (var row = 0; row < deletion.KeyValues.GetLength(0); row++)
                    {
                        var permission = deletion.KeyValues[row, permissionIndex]?.ToString();
                        var account = deletion.KeyValues[row, accountIndex]?.ToString();
                        if (permission is null || account is null) continue;
                        var helper = _dependencies.SqlGenerationHelper;
                        expanded.Add(new SqlOperation { Sql = $"DELETE FROM {helper.DelimitIdentifier("role_permissions", deletion.Schema)} " +
                            $"WHERE {helper.DelimitIdentifier("permission_id")} = {mapping.GenerateSqlLiteral(permission)} " +
                            $"AND {helper.DelimitIdentifier("account_type")} = {mapping.GenerateSqlLiteral(account)}{helper.StatementTerminator}" });
                    }
                }
            }
            if (operation is DeleteDataOperation seedDeletion && seedDeletion.KeyColumnTypes is null &&
                seedDeletion.Table is "permission_account_types" or "permissions" &&
                seedDeletion.KeyColumns.All(c => c is "permission_id" or "account_type"))
            {
                // Some legacy Down target models omit these global seed entities.
                // Supply their stable SQL key types without altering migration history.
                expanded.Add(new DeleteDataOperation { Table=seedDeletion.Table,Schema=seedDeletion.Schema,
                    KeyColumns=seedDeletion.KeyColumns,KeyValues=seedDeletion.KeyValues,
                    KeyColumnTypes=seedDeletion.KeyColumns.Select(c=>c=="permission_id"?"varchar(36)":"varchar(32)").ToArray() });
            }
            else expanded.Add(operation);
        }
        return _providerGenerator.Generate(expanded, model, options);
    }
}
