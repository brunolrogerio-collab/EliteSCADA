using Scada.Core.Tags;
using Scada.Drivers.OpcUa;

namespace Scada.Drivers.Tests;

public sealed class OpcUaRuntimeContractsTests
{
    [Fact]
    public void Binding_UsesImportMetadataAndRuntimeDefaults()
    {
        var tag = CreateTag(
            TagDataType.Double,
            readOnly: false,
            new Dictionary<string, string>
            {
                ["opcUa.nodeId"] = "ns=2;s=Motor.Speed",
                ["opcUa.namespaceUri"] = "urn:elite:line-a"
            });

        var binding = OpcUaRuntimeBinding.FromTag(tag);

        Assert.Equal("ns=2;s=Motor.Speed", binding.Node.NodeId);
        Assert.Equal("urn:elite:line-a", binding.Node.NamespaceUri);
        Assert.Equal(TimeSpan.FromSeconds(1), binding.SamplingInterval);
        Assert.Equal(1u, binding.QueueSize);
        Assert.True(binding.DiscardOldest);
        Assert.True(binding.Writable);
    }

    [Fact]
    public void Binding_RejectsInvalidQueueSize()
    {
        var tag = CreateTag(
            TagDataType.Double,
            readOnly: false,
            new Dictionary<string, string>
            {
                ["opcUa.nodeId"] = "ns=2;s=Motor.Speed",
                ["opcUa.queueSize"] = "10001"
            });

        var error = Assert.Throws<InvalidOperationException>(() => OpcUaRuntimeBinding.FromTag(tag));

        Assert.Contains("queueSize", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteValidation_IsStrictAndRejectsReadOnlyTags()
    {
        var writable = CreateTag(TagDataType.Double, readOnly: false);
        var readOnly = CreateTag(TagDataType.Double, readOnly: true);

        OpcUaRuntimeValueValidator.ValidateWrite(writable, 12.5d);
        Assert.Throws<ArgumentException>(() => OpcUaRuntimeValueValidator.ValidateWrite(writable, 12.5f));
        Assert.Throws<InvalidOperationException>(() => OpcUaRuntimeValueValidator.ValidateWrite(readOnly, 12.5d));
    }

    private static TagDefinition CreateTag(
        TagDataType dataType,
        bool readOnly,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        metadata ??= new Dictionary<string, string>
        {
            ["opcUa.nodeId"] = "ns=2;s=Test.Value"
        };
        return TagDefinition.Create("Value", "Area.Value", dataType, readOnly: readOnly, metadata: metadata);
    }
}
