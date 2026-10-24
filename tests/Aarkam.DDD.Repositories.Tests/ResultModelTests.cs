using Aarkam.DDD.Domain;
using Aarkam.Domain.Repositories;
using Xunit;

namespace Aarkam.DDD.Repositories.Tests;

public class CustomTestDomainException : DomainException
{
    public CustomTestDomainException(
        string message, 
        string errorCode = "TEST_ERR", 
        ExceptionSeverity severity = ExceptionSeverity.Error, 
        IDictionary<string, object?>? details = null) 
        : base(message, errorCode, severity, null, details)
    {
    }
}

public class ResultModelTests
{
    // =========================================================================
    // 1. ResultModel Non-Generic Success Tests
    // =========================================================================

    [Fact]
    public void Success_Default_ShouldReturnSuccessfulResult()
    {
        var result = ResultModel.Success();

        Assert.True(result.IsSuccess);
        Assert.Equal("S000", result.ResponseCode);
        Assert.Equal("S000", result.ErrorCode);
        Assert.Equal("Operation completed successfully.", result.Message);
        Assert.Empty(result.Errors);
        Assert.Null(result.Severity);
        Assert.Null(result.Details);
    }

    [Theory]
    [InlineData("Entity saved.")]
    [InlineData("Customer updated.")]
    [InlineData("Order processed.")]
    [InlineData("Operation finished.")]
    public void Success_WithCustomMessage_ShouldPreserveMessage(string message)
    {
        var result = ResultModel.Success(message);

        Assert.True(result.IsSuccess);
        Assert.Equal(message, result.Message);
        Assert.Equal("S000", result.ResponseCode);
        Assert.Equal("S000", result.ErrorCode);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("SUCCESS_200", "Saved")]
    [InlineData("OK_CREATED", "Created")]
    [InlineData("S001", "Completed with code 1")]
    [InlineData("S999", "Special success")]
    public void Success_WithCustomResponseCode_ShouldSetCodes(string code, string message)
    {
        var result = ResultModel.Success(message, code);

        Assert.True(result.IsSuccess);
        Assert.Equal(code, result.ResponseCode);
        Assert.Equal(code, result.ErrorCode);
        Assert.Equal(message, result.Message);
    }

    // =========================================================================
    // 2. ResultModel Non-Generic Failure Tests
    // =========================================================================

    [Fact]
    public void Failure_Default_ShouldSetStandardErrorProperties()
    {
        var result = ResultModel.Failure();

        Assert.False(result.IsSuccess);
        Assert.Equal("E500", result.ResponseCode);
        Assert.Equal("E500", result.ErrorCode);
        Assert.Equal("Operation failed.", result.Message);
        Assert.Empty(result.Errors);
        Assert.Null(result.Severity);
        Assert.Null(result.Details);
    }

    [Theory]
    [InlineData("Custom error", "E400")]
    [InlineData("Validation failed", "VALIDATION_FAILED")]
    [InlineData("NotFound", "ERR_404")]
    [InlineData("Unauthorized", "AUTH_DENIED")]
    [InlineData("Conflict detected", "CONFLICT_409")]
    public void Failure_WithMessageAndCode_ShouldSetCorrectly(string message, string code)
    {
        var result = ResultModel.Failure(message, code);

        Assert.False(result.IsSuccess);
        Assert.Equal(message, result.Message);
        Assert.Equal(code, result.ResponseCode);
        Assert.Equal(code, result.ErrorCode);
    }

    [Theory]
    [InlineData(ExceptionSeverity.Warning)]
    [InlineData(ExceptionSeverity.Error)]
    [InlineData(ExceptionSeverity.Critical)]
    public void Failure_WithSeverity_ShouldReflectSeverity(ExceptionSeverity severity)
    {
        var result = ResultModel.Failure("Failed", "E500", severity);

        Assert.False(result.IsSuccess);
        Assert.Equal(severity, result.Severity);
    }

    [Fact]
    public void Failure_WithDetails_ShouldMakeImmutableCopy()
    {
        var dict = new Dictionary<string, object?>
        {
            ["TenantId"] = "tenant-123",
            ["RetryCount"] = 3,
            ["TraceId"] = Guid.NewGuid()
        };

        var result = ResultModel.Failure("Failed", "E500", ExceptionSeverity.Error, dict);

        Assert.NotNull(result.Details);
        Assert.Equal(3, result.Details.Count);
        Assert.Equal("tenant-123", result.Details["TenantId"]);
        Assert.Equal(3, result.Details["RetryCount"]);

        // Mutating original dictionary should not affect result.Details
        dict["TenantId"] = "mutated";
        Assert.Equal("tenant-123", result.Details["TenantId"]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    public void Failure_WithMultipleErrors_ShouldPopulateErrorsList(int errorCount)
    {
        var errors = Enumerable.Range(1, errorCount).Select(i => (object)$"Error #{i}").ToList();
        var result = ResultModel.Failure(errors, "E400");

        Assert.False(result.IsSuccess);
        Assert.Equal(errorCount, result.Errors.Count);
        Assert.Equal("E400", result.ResponseCode);
    }

    [Fact]
    public void Failure_WithSingleErrorObject_ShouldPopulateErrorsList()
    {
        var result = ResultModel.Failure((object)"Single error happened", "E422", ExceptionSeverity.Warning);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Errors);
        Assert.Equal("Single error happened", result.Errors[0]);
        Assert.Equal("E422", result.ResponseCode);
        Assert.Equal(ExceptionSeverity.Warning, result.Severity);
    }

    // =========================================================================
    // 3. ResultModel FromException Integration Tests
    // =========================================================================

    [Fact]
    public void FromException_DomainException_ShouldMapAllFields()
    {
        var details = new Dictionary<string, object?> { ["Key"] = "Val", ["Attempt"] = 1 };
        var domainEx = new CustomTestDomainException(
            "Domain invariant violated", 
            "ORDER_ALREADY_SHIPPED", 
            ExceptionSeverity.Critical, 
            details);

        var result = ResultModel.FromException(domainEx);

        Assert.False(result.IsSuccess);
        Assert.Equal("Domain invariant violated", result.Message);
        Assert.Equal("ORDER_ALREADY_SHIPPED", result.ResponseCode);
        Assert.Equal("ORDER_ALREADY_SHIPPED", result.ErrorCode);
        Assert.Equal(ExceptionSeverity.Critical, result.Severity);
        Assert.NotNull(result.Details);
        Assert.Equal("Val", result.Details["Key"]);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void FromException_StandardException_ShouldMapWithDefaults()
    {
        var ex = new InvalidOperationException("Invalid state transition");
        var result = ResultModel.FromException(ex);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid state transition", result.Message);
        Assert.Equal("E500", result.ResponseCode);
        Assert.Equal("E500", result.ErrorCode);
        Assert.Equal(ExceptionSeverity.Critical, result.Severity);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void FromException_NullDomainException_ShouldThrowArgumentNullException()
    {
        DomainException? ex = null;
        Assert.Throws<ArgumentNullException>(() => ResultModel.FromException(ex!));
    }

    [Fact]
    public void FromException_NullException_ShouldThrowArgumentNullException()
    {
        Exception? ex = null;
        Assert.Throws<ArgumentNullException>(() => ResultModel.FromException(ex!));
    }

    // =========================================================================
    // 4. ResultModel<TData> Generic Success Tests
    // =========================================================================

    [Theory]
    [InlineData(100)]
    [InlineData(0)]
    [InlineData(-50)]
    [InlineData(int.MaxValue)]
    public void GenericSuccess_IntData_ShouldReturnData(int value)
    {
        var result = ResultModel<int>.Success(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Data);
        Assert.Equal("S000", result.ResponseCode);
    }

    [Theory]
    [InlineData("Alice")]
    [InlineData("Bob")]
    [InlineData("")]
    [InlineData("Special characters: @#$%^&*()_+")]
    public void GenericSuccess_StringData_ShouldReturnData(string value)
    {
        var result = ResultModel<string>.Success(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Data);
    }

    [Fact]
    public void GenericSuccess_ComplexObject_ShouldReturnObject()
    {
        var obj = new { Id = Guid.NewGuid(), Name = "Widget" };
        var result = ResultModel<object>.Success(obj);

        Assert.True(result.IsSuccess);
        Assert.Same(obj, result.Data);
    }

    [Fact]
    public void GenericSuccess_WithCustomMessageAndCode_ShouldPreserveBoth()
    {
        var result = ResultModel<string>.Success("Data payload", "Custom success message", "S201");

        Assert.True(result.IsSuccess);
        Assert.Equal("Data payload", result.Data);
        Assert.Equal("Custom success message", result.Message);
        Assert.Equal("S201", result.ResponseCode);
        Assert.Equal("S201", result.ErrorCode);
    }

    // =========================================================================
    // 5. ResultModel<TData> Generic Failure Tests
    // =========================================================================

    [Fact]
    public void GenericFailure_Default_ShouldHaveDefaultTData()
    {
        var result = ResultModel<int>.Failure();

        Assert.False(result.IsSuccess);
        Assert.Equal(0, result.Data);
        Assert.Equal("E500", result.ResponseCode);
    }

    [Theory]
    [InlineData("Failed to load entity", "NOT_FOUND")]
    [InlineData("Validation error", "VAL_01")]
    [InlineData("Timeout reached", "TIMEOUT")]
    public void GenericFailure_WithMessageAndCode_ShouldSetProperly(string message, string code)
    {
        var result = ResultModel<string?>.Failure(message, code);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(message, result.Message);
        Assert.Equal(code, result.ResponseCode);
        Assert.Equal(code, result.ErrorCode);
    }

    [Theory]
    [InlineData(ExceptionSeverity.Warning)]
    [InlineData(ExceptionSeverity.Error)]
    [InlineData(ExceptionSeverity.Critical)]
    public void GenericFailure_WithSeverity_ShouldReflectSeverity(ExceptionSeverity severity)
    {
        var result = ResultModel<string?>.Failure("Error", "E500", severity);

        Assert.False(result.IsSuccess);
        Assert.Equal(severity, result.Severity);
    }

    [Fact]
    public void GenericFailure_WithDetails_ShouldMapDetails()
    {
        var dict = new Dictionary<string, object?> { ["Field"] = "Email", ["Reason"] = "InvalidFormat" };
        var result = ResultModel<string?>.Failure("Validation failed", "E400", ExceptionSeverity.Warning, dict);

        Assert.NotNull(result.Details);
        Assert.Equal("Email", result.Details["Field"]);
        Assert.Equal("InvalidFormat", result.Details["Reason"]);
    }

    // =========================================================================
    // 6. ResultModel<TData> FromValue Tests
    // =========================================================================

    [Fact]
    public void FromValue_NonNullReferenceType_ShouldReturnSuccess()
    {
        var payload = "Valid String";
        var result = ResultModel<string>.FromValue(payload);

        Assert.True(result.IsSuccess);
        Assert.Equal("Valid String", result.Data);
    }

    [Fact]
    public void FromValue_NullReferenceType_ShouldReturnFailure()
    {
        string? payload = null;
        var result = ResultModel<string?>.FromValue(payload, "Item was not found", "E404");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal("Item was not found", result.Message);
        Assert.Equal("E404", result.ResponseCode);
        Assert.Equal("E404", result.ErrorCode);
    }

    [Fact]
    public void FromValue_NullableValueType_WithValue_ShouldReturnSuccess()
    {
        int? number = 42;
        var result = ResultModel<int?>.FromValue(number);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Data);
    }

    [Fact]
    public void FromValue_NullableValueType_Null_ShouldReturnFailure()
    {
        int? number = null;
        var result = ResultModel<int?>.FromValue(number);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal("Resource not found", result.Message);
        Assert.Equal("E404", result.ResponseCode);
    }

    // =========================================================================
    // 7. ResultModel<TData> Generic FromException Tests
    // =========================================================================

    [Fact]
    public void GenericFromException_DomainException_ShouldMapProperly()
    {
        var domainEx = new CustomTestDomainException(
            "Account locked", 
            "ACCOUNT_LOCKED", 
            ExceptionSeverity.Warning,
            new Dictionary<string, object?> { ["LockDuration"] = 30 });

        var result = ResultModel<string?>.FromException(domainEx);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal("Account locked", result.Message);
        Assert.Equal("ACCOUNT_LOCKED", result.ErrorCode);
        Assert.Equal(ExceptionSeverity.Warning, result.Severity);
        Assert.NotNull(result.Details);
        Assert.Equal(30, result.Details["LockDuration"]);
    }

    [Fact]
    public void GenericFromException_StandardException_ShouldMapDefaults()
    {
        var ex = new ArgumentException("Parameter cannot be null", "username");
        var result = ResultModel<int>.FromException(ex);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, result.Data);
        Assert.Equal("Parameter cannot be null (Parameter 'username')", result.Message);
        Assert.Equal("E500", result.ErrorCode);
        Assert.Equal(ExceptionSeverity.Critical, result.Severity);
    }
}
