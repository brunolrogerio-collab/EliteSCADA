namespace Scada.Drivers.AllenBradley;

/// <summary>
/// Stable public identities owned by the Allen-Bradley Logix driver contract.
/// These values are library-independent and are the protocol-owned projection
/// targeted by the shared rich communication-binding convergence contract.
/// </summary>
public static class AllenBradleyLogixContractIdentity
{
    public const string DriverType = AllenBradleyLogixEngineeringAdapter.DriverType;
    public const int DriverContractVersion = 1;
    public const string BindingSchemaId = "elitescada.driver.rockwell.logix.eip";
    public const int BindingSchemaVersion = 1;
    public const string PortableAddressPrefix = LogixPortableAddress.Prefix;
}
