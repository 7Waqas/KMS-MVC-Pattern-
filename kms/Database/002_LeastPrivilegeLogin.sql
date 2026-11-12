/* =====================================================================
   KMS — dedicated low-privilege SQL login
   ---------------------------------------------------------------------
   Today KMS connects as `faces`, which is a SYSADMIN (full control of the
   whole SQL Server). This script creates a login that can only do what
   KMS needs.

   HOW TO USE (on the server, as an administrator):
     1. Replace  <STRONG_PASSWORD_HERE>  with a new strong password.
     2. Run the script against the server.
     3. In KMS appsettings.json change the connection string to
        User Id=kms_app;Password=<the new password>
     4. Test KMS. Do NOT change or disable `faces` — the attendance
        software may use it.
   ===================================================================== */
USE master;
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'kms_app')
    CREATE LOGIN kms_app WITH PASSWORD = N'<STRONG_PASSWORD_HERE>', CHECK_POLICY = ON, DEFAULT_DATABASE = AASFF;
GO

USE AASFF;
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'kms_app')
    CREATE USER kms_app FOR LOGIN kms_app;
GO

-- Read-only access to the device data
GRANT SELECT ON dbo.tmpTRecords      TO kms_app;
GRANT SELECT ON dbo.tblzEnrollDFace  TO kms_app;

-- KMS master data (managed from the KMS screens)
GRANT SELECT, INSERT, UPDATE         ON dbo.EmployeeMaster   TO kms_app;
GRANT SELECT, INSERT, UPDATE         ON dbo.KeyMaster        TO kms_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.KeyAuthorization TO kms_app;
GRANT SELECT                         ON dbo.KeyReportData    TO kms_app;

-- Key Custody tables (written by the KMS background processor)
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.KcEvent            TO kms_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.KcIncident         TO kms_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.KcIncidentSuspect  TO kms_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.KcProcessedDay     TO kms_app;
GO
PRINT 'kms_app login ready.';
