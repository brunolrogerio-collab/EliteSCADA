using Scada.Drivers.Abstractions;
using Scada.Drivers.Dnp3;

namespace Scada.Drivers.Tests;

public sealed class Dnp3DataSourceSettingsParserTests
{
    [Fact]
    public void Parse_MinimumRequiredSettings_UsesDocumentedDefaults()
    {
        var result = Dnp3DataSourceSettingsParser.Parse(BaseSettings());

        Assert.True(result.Succeeded);
        Assert.Empty(result.Issues);
        var value = Assert.IsType<Dnp3ParsedDataSourceSettings>(result.Value);
        Assert.Equal("192.0.2.10", value.Connection.Host);
        Assert.Equal(20000, value.Connection.Port);
        Assert.Equal((ushort)1, value.Connection.MasterAddress);
        Assert.Equal((ushort)1024, value.Connection.OutstationAddress);
        Assert.Equal(TimeSpan.FromSeconds(5), value.Connection.ConnectTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), value.Association.ResponseTimeout);
        Assert.Equal(TimeSpan.FromSeconds(1), value.Association.ReconnectMinDelay);
        Assert.Equal(TimeSpan.FromSeconds(30), value.Association.ReconnectMaxDelay);
        Assert.Equal(TimeSpan.FromSeconds(60), value.Association.KeepAliveTimeout);
        Assert.Equal(TimeSpan.FromMinutes(15), value.Association.IntegrityPollInterval);
        Assert.Equal(Dnp3ClassSet.All, value.Association.StartupIntegrityClasses);
        Assert.Equal(Dnp3ClassSet.EventClasses, value.Association.DisableUnsolicitedClassesOnStartup);
        Assert.Equal(Dnp3ClassSet.EventClasses, value.Association.EnableUnsolicitedClassesAfterIntegrity);
        Assert.Equal(Dnp3ClassSet.EventClasses, value.Association.EventScanOnEventsAvailable);
        Assert.Equal(Dnp3TimeSyncMode.Disabled, value.Association.TimeSyncMode);
        Assert.Equal(16, value.Association.MaxQueuedUserRequests);
    }

    [Fact]
    public void Parse_ClassFlagsAndOptionalDurations_AreCompiledDeterministically()
    {
        var settings = BaseSettings();
        settings["port"] = "21000";
        settings["connectTimeout"] = "00:00:03";
        settings["keepAliveTimeout"] = "";
        settings["class1PollInterval"] = "00:00:10";
        settings["startupIntegrityClass1"] = "false";
        settings["startupIntegrityClass3"] = "false";
        settings["disableUnsolicitedClass1OnStartup"] = "false";
        settings["disableUnsolicitedClass3OnStartup"] = "false";
        settings["enableUnsolicitedClass2AfterIntegrity"] = "false";
        settings["eventScanClass1OnEventsAvailable"] = "false";
        settings["eventScanClass2OnEventsAvailable"] = "false";
        settings["integrityOnEventBufferOverflow"] = "false";
        settings["timeSyncMode"] = "nonLan";
        settings["maxQueuedUserRequests"] = "32";

        var result = Dnp3DataSourceSettingsParser.Parse(settings);

        Assert.True(result.Succeeded);
        var value = Assert.IsType<Dnp3ParsedDataSourceSettings>(result.Value);
        Assert.Equal(21000, value.Connection.Port);
        Assert.Equal(TimeSpan.FromSeconds(3), value.Connection.ConnectTimeout);
        Assert.Null(value.Association.KeepAliveTimeout);
        Assert.Equal(TimeSpan.FromSeconds(10), value.Association.Class1PollInterval);
        Assert.Equal(Dnp3ClassSet.Class0 | Dnp3ClassSet.Class2, value.Association.StartupIntegrityClasses);
        Assert.Equal(Dnp3ClassSet.Class2, value.Association.DisableUnsolicitedClassesOnStartup);
        Assert.Equal(Dnp3ClassSet.Class1 | Dnp3ClassSet.Class3, value.Association.EnableUnsolicitedClassesAfterIntegrity);
        Assert.Equal(Dnp3ClassSet.Class3, value.Association.EventScanOnEventsAvailable);
        Assert.False(value.Association.IntegrityOnEventBufferOverflow);
        Assert.Equal(Dnp3TimeSyncMode.NonLan, value.Association.TimeSyncMode);
        Assert.Equal(32, value.Association.MaxQueuedUserRequests);
    }

    [Theory]
    [InlineData("transport")]
    [InlineData("host")]
    [InlineData("masterAddress")]
    [InlineData("outstationAddress")]
    public void Parse_MissingRequiredSetting_FailsWithFieldIssue(string missingKey)
    {
        var settings = BaseSettings();
        settings.Remove(missingKey);

        var result = Dnp3DataSourceSettingsParser.Parse(settings);

        Assert.False(result.Succeeded);
        Assert.Null(result.Value);
        Assert.Contains(result.Issues, issue => issue.Severity == DriverEngineeringIssueSeverity.Error && issue.FieldKey == missingKey);
    }

    [Fact]
    public void Parse_InvalidTransportDurationAndQueue_FailClosed()
    {
        var settings = BaseSettings();
        settings["transport"] = "serial";
        settings["responseTimeout"] = "5 seconds";
        settings["maxQueuedUserRequests"] = "0";

        var result = Dnp3DataSourceSettingsParser.Parse(settings);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, issue => issue.Code == "DNP3_TRANSPORT_UNSUPPORTED");
        Assert.Contains(result.Issues, issue => issue.Code == "DNP3_SETTING_INVALID" && issue.FieldKey == "responseTimeout");
        Assert.Contains(result.Issues, issue => issue.Code == "DNP3_SETTING_INVALID" && issue.FieldKey == "maxQueuedUserRequests");
    }

    [Fact]
    public void Parse_EqualLinkAddressesAndNoIntegrityClass_FailValidation()
    {
        var settings = BaseSettings();
        settings["outstationAddress"] = "1";
        settings["startupIntegrityClass0"] = "false";
        settings["startupIntegrityClass1"] = "false";
        settings["startupIntegrityClass2"] = "false";
        settings["startupIntegrityClass3"] = "false";

        var result = Dnp3DataSourceSettingsParser.Parse(settings);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, issue => issue.Code == "DNP3_CONNECTION_INVALID" && issue.FieldKey == "outstationAddress");
        Assert.Contains(result.Issues, issue => issue.Code == "DNP3_ASSOCIATION_INVALID");
    }

    [Fact]
    public void Parse_UnknownSetting_IsWarningOnly()
    {
        var settings = BaseSettings();
        settings["futureOption"] = "value";

        var result = Dnp3DataSourceSettingsParser.Parse(settings);

        Assert.True(result.Succeeded);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("DNP3_SETTING_UNKNOWN", issue.Code);
        Assert.Equal(DriverEngineeringIssueSeverity.Warning, issue.Severity);
        Assert.Equal("futureOption", issue.FieldKey);
    }

    [Fact]
    public void Parse_DuplicateKeyIgnoringCase_FailsDeterministically()
    {
        var settings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["transport"] = "tcp",
            ["host"] = "192.0.2.10",
            ["Host"] = "192.0.2.11",
            ["masterAddress"] = "1",
            ["outstationAddress"] = "2"
        };

        var result = Dnp3DataSourceSettingsParser.Parse(settings);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, issue => issue.Code == "DNP3_SETTING_DUPLICATE");
    }

    private static Dictionary<string, string> BaseSettings() => new()
    {
        ["transport"] = "tcp",
        ["host"] = "192.0.2.10",
        ["masterAddress"] = "1",
        ["outstationAddress"] = "1024"
    };
}
