/* =====================================================================
   TEST — REMOVE all test data created by TEST_1 / TEST_2 (laptop only).
   ===================================================================== */
SET NOCOUNT ON;

DECLARE @days TABLE (d date PRIMARY KEY);
INSERT INTO @days SELECT DISTINCT CAST(KqDate AS date) FROM dbo.tmpTRecords WHERE sign_cause = 'KMS-TEST';

DELETE FROM dbo.tmpTRecords WHERE sign_cause = 'KMS-TEST';
PRINT CONCAT('Removed ', @@ROWCOUNT, ' test scans.');

-- Remove what the processor / old trigger built for those days, but only for days
-- that have no real scans left.
DELETE d FROM @days d
WHERE EXISTS (SELECT 1 FROM dbo.tmpTRecords t WHERE CAST(t.KqDate AS date) = d.d);

DELETE FROM dbo.KcEvent        WHERE ScanDate     IN (SELECT d FROM @days);
DELETE FROM dbo.KcIncident     WHERE IncidentDate IN (SELECT d FROM @days);   -- suspects cascade
DELETE FROM dbo.KcProcessedDay WHERE Day          IN (SELECT d FROM @days);
DELETE FROM dbo.KeyReportData  WHERE ReportDate   IN (SELECT d FROM @days);   -- rows written by the old trigger

PRINT 'Test results removed.';
