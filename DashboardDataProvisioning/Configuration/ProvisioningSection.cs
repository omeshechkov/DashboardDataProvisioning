using System.Configuration;

namespace DashboardDataProvisioning.Configuration
{
    public class ProvisioningSection: ConfigurationSection
    {
        public const string Name = "provisioning";

        [ConfigurationProperty("scenarios", IsRequired = true)]
        public ScenariosElement Scenarios
        {
            get { return (ScenariosElement)this["scenarios"]; }
            set { this["scenarios"] = value; }
        }
    }
}