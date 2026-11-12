/* =====================================================================
   KMS — Key Custody Intelligence tables
   ---------------------------------------------------------------------
   * Safe to run more than once (creates only what is missing).
   * Does NOT modify any existing table (tmpTRecords, KeyMaster, ...).
   * Filled automatically by the KMS background processor, which reads
     tmpTRecords. Take a database backup before running on production.
   ===================================================================== */
SET NOCOUNT ON;
GO

/* One row per KEY scan, paired with the face scan of the person at the
   board, and with the direction worked out from the key's custody. */
IF OBJECT_ID(N'dbo.KcEvent', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KcEvent
    (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_KcEvent PRIMARY KEY,
        ScanDate        DATE           NOT NULL,
        ScanTime        DATETIME2(0)   NOT NULL,
        SourceId        INT            NOT NULL,          -- tmpTRecords.ID
        KeyEnroll       INT            NOT NULL,
        KeyName         NVARCHAR(100)  NOT NULL,
        Direction       NVARCHAR(3)    NOT NULL,          -- OUT / IN
        Result          NVARCHAR(20)   NOT NULL,          -- OK / NOT_AUTHORIZED / NO_FACE
        FaceEnroll      INT            NULL,
        FaceName        NVARCHAR(100)  NULL,
        FaceOffsetSec   INT            NULL,              -- face time minus key time
        ProcessedAt     DATETIME2(0)   NOT NULL CONSTRAINT DF_KcEvent_ProcessedAt DEFAULT (SYSDATETIME())
    );
    CREATE INDEX IX_KcEvent_ScanDate ON dbo.KcEvent (ScanDate, KeyEnroll, ScanTime);
END
GO

/* One row per security incident (a bypass or a violation). */
IF OBJECT_ID(N'dbo.KcIncident', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KcIncident
    (
        Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_KcIncident PRIMARY KEY,
        NaturalKey        NVARCHAR(120)  NOT NULL,          -- stable id so re-processing never duplicates
        IncidentDate      DATE           NOT NULL,
        Type              NVARCHAR(30)   NOT NULL,          -- NOT_AUTHORIZED / NO_FACE / TAKEN_WITHOUT_SCAN / RETURN_NOT_SCANNED / SILENT_KEY
        Severity          TINYINT        NOT NULL,          -- 1 = high, 2 = medium, 3 = low
        KeyEnroll         INT            NOT NULL,
        KeyName           NVARCHAR(100)  NOT NULL,
        OccurredAt        DATETIME2(0)   NULL,
        PersonEnroll      INT            NULL,              -- person directly responsible (if known)
        PersonName        NVARCHAR(100)  NULL,
        Summary           NVARCHAR(500)  NOT NULL,
        AuthorizedPersons NVARCHAR(500)  NULL,
        Status            NVARCHAR(20)   NOT NULL CONSTRAINT DF_KcIncident_Status DEFAULT (N'Open'),  -- Open / Confirmed / Dismissed
        ReviewNote        NVARCHAR(500)  NULL,
        ReviewedAt        DATETIME2(0)   NULL,
        CreatedAt         DATETIME2(0)   NOT NULL CONSTRAINT DF_KcIncident_CreatedAt DEFAULT (SYSDATETIME())
    );
    CREATE UNIQUE INDEX UX_KcIncident_NaturalKey ON dbo.KcIncident (NaturalKey);
    CREATE INDEX IX_KcIncident_Date ON dbo.KcIncident (IncidentDate, Severity);
END
GO

/* Ranked list of people who were at the board when an incident happened. */
IF OBJECT_ID(N'dbo.KcIncidentSuspect', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KcIncidentSuspect
    (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_KcIncidentSuspect PRIMARY KEY,
        IncidentId    INT            NOT NULL CONSTRAINT FK_KcIncidentSuspect_Incident
                                     REFERENCES dbo.KcIncident (Id) ON DELETE CASCADE,
        Rank          INT            NOT NULL,
        EmpEnroll     INT            NOT NULL,
        EmpName       NVARCHAR(100)  NOT NULL,
        IsAuthorized  BIT            NOT NULL,
        Reason        NVARCHAR(200)  NOT NULL,
        SeenAt        DATETIME2(0)   NULL
    );
    CREATE INDEX IX_KcIncidentSuspect_Incident ON dbo.KcIncidentSuspect (IncidentId, Rank);
END
GO

/* Bookkeeping: which days have been processed and whether new scans arrived since. */
IF OBJECT_ID(N'dbo.KcProcessedDay', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KcProcessedDay
    (
        Day          DATE          NOT NULL CONSTRAINT PK_KcProcessedDay PRIMARY KEY,
        SourceRows   INT           NOT NULL,
        MaxSourceId  INT           NOT NULL,
        Settled      BIT           NOT NULL,
        ProcessedAt  DATETIME2(0)  NOT NULL
    );
END
GO

PRINT 'Key Custody tables are ready.';
