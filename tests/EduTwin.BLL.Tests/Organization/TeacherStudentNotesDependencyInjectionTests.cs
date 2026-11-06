using EduTwin.API.Controllers;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

public sealed class TeacherStudentNotesDependencyInjectionTests
{
    [Fact]
    public void ExistingModuleRegistrations_ResolveTheEntireNotesController()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<EduTwinDbContext>(options => options.UseInMemoryDatabase("notes-controller-di"));
        services.AddIdentityAndTenancy();
        services.AddOrganization();
        services.AddTransient<TeacherStudentNotesController>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<TeacherStudentNotesController>());
    }
}
