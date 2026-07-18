using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Xunit;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public class LoginRequestValidationTests
{
    private static List<ValidationResult> Validate(LoginRequest request)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(request);
        Validator.TryValidateObject(request, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Valid_Request_NoErrors()
    {
        var request = new LoginRequest
        {
            CenterCode = "EDU-A",
            Username = "manager.a",
            Password = "password"
        };

        var errors = Validate(request);
        Assert.Empty(errors);
    }

    [Fact]
    public void CenterCode_Empty_ReturnsVietnameseRequired()
    {
        var request = new LoginRequest
        {
            CenterCode = "",
            Username = "manager",
            Password = "password"
        };

        var errors = Validate(request);
        Assert.Contains(errors, e =>
            e.MemberNames.Contains("CenterCode") &&
            e.ErrorMessage == "Mã trung tâm là bắt buộc.");
    }

    [Fact]
    public void CenterCode_TooLong_ReturnsVietnameseMaxLength()
    {
        var request = new LoginRequest
        {
            CenterCode = new string('A', 33),
            Username = "manager",
            Password = "password"
        };

        var errors = Validate(request);
        Assert.Contains(errors, e =>
            e.MemberNames.Contains("CenterCode") &&
            e.ErrorMessage == "Mã trung tâm không được vượt quá 32 ký tự.");
    }

    [Fact]
    public void Username_Empty_ReturnsVietnameseRequired()
    {
        var request = new LoginRequest
        {
            CenterCode = "EDU-A",
            Username = "",
            Password = "password"
        };

        var errors = Validate(request);
        Assert.Contains(errors, e =>
            e.MemberNames.Contains("Username") &&
            e.ErrorMessage == "Tên đăng nhập là bắt buộc.");
    }

    [Fact]
    public void Username_TooLong_ReturnsVietnameseMaxLength()
    {
        var request = new LoginRequest
        {
            CenterCode = "EDU-A",
            Username = new string('u', 101),
            Password = "password"
        };

        var errors = Validate(request);
        Assert.Contains(errors, e =>
            e.MemberNames.Contains("Username") &&
            e.ErrorMessage == "Tên đăng nhập không được vượt quá 100 ký tự.");
    }

    [Fact]
    public void Password_Empty_ReturnsVietnameseRequired()
    {
        var request = new LoginRequest
        {
            CenterCode = "EDU-A",
            Username = "manager",
            Password = ""
        };

        var errors = Validate(request);
        Assert.Contains(errors, e =>
            e.MemberNames.Contains("Password") &&
            e.ErrorMessage == "Mật khẩu là bắt buộc.");
    }
}
