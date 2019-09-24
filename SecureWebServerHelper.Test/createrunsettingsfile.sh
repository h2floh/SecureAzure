# Write your commands here

echo 'Adding Environment Variables'

cat > .runsettings << ENDOFFILE
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <!-- Parameters used by tests at runtime -->
  <TestRunParameters>
    <Parameter name="KeyVaultName" value="$KeyVaultName" />
    <Parameter name="CertificateName" value="$CertificateName" />
    <Parameter name="AzureConnectionString" value="$AzureConnectionString" />
	<Parameter name="CertificateThumbprint" value="$CertificateThumbprint" />
  </TestRunParameters>
</RunSettings>
ENDOFFILE

echo .runsettings