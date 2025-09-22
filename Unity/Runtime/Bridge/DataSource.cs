namespace Aqua.Runtime
{
    public enum DataSource : int
    {
        Production,
        Staging,
        Legacy,
        Unstable,
        Loopback,
        Local,
        LocalTest,
        LocalFQDN,
        LocalFQDNTest,
        HpaNginxLaptop
    }
}