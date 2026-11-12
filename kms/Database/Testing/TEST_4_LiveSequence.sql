/* =====================================================================
   TEST — LIVE ALERT SEQUENCE (laptop only).
   Keep a KMS page open (click it once so sound is allowed), then run.
   Sends 3 bad scans, 30 seconds apart. Each should pop up as a red alert.
   Remove afterwards with TEST_3_RemoveTestData.sql.
   ===================================================================== */
SET NOCOUNT ON;
IF EXISTS (SELECT 1 FROM dbo.tmpTRecords
           WHERE CAST(KqDate AS date) = CAST(GETDATE() AS date) AND ISNULL(sign_cause, '') <> 'KMS-TEST')
BEGIN
    RAISERROR('Real scans exist for today — this looks like a live database. Nothing inserted.', 16, 1);
    RETURN;
END

DECLARE @now datetime;

-- 1) Key RO(M&E)-1 scanned with NO face
SET @now = GETDATE();
INSERT INTO dbo.tmpTRecords (clock_id, card_id, emp_id, KqDate, KqTime, mark, flag, sign_cause)
VALUES (1, '0000001099', '00001099', CAST(CAST(@now AS date) AS datetime), CAST(CAST(@now AS time(0)) AS datetime), 0, 0, 'KMS-TEST');
PRINT CONCAT(CONVERT(varchar(8), @now, 108), '  1) RO(M&E)-1 scanned with NO face');
RAISERROR('', 0, 1) WITH NOWAIT;
WAITFOR DELAY '00:00:30';

-- 2) Muhammad Hamza Ch (NOT authorized) takes key AC(M&E)-1
SET @now = GETDATE();
INSERT INTO dbo.tmpTRecords (clock_id, card_id, emp_id, KqDate, KqTime, mark, flag, sign_cause)
VALUES (1, '0000000117', '00000117', CAST(CAST(@now AS date) AS datetime), CAST(CAST(@now AS time(0)) AS datetime), 0, 0, 'KMS-TEST'),
       (1, '0000001098', '00001098', CAST(CAST(@now AS date) AS datetime), CAST(CAST(DATEADD(SECOND, 4, @now) AS time(0)) AS datetime), 0, 0, 'KMS-TEST');
PRINT CONCAT(CONVERT(varchar(8), @now, 108), '  2) Hamza (not authorized) took AC(M&E)-1');
RAISERROR('', 0, 1) WITH NOWAIT;
WAITFOR DELAY '00:00:30';

-- 3) Shahbaz (NOT authorized) takes key D-11-2 ... use key_ASMF again: Shahbaz scans face + ASMF
SET @now = GETDATE();
INSERT INTO dbo.tmpTRecords (clock_id, card_id, emp_id, KqDate, KqTime, mark, flag, sign_cause)
VALUES (1, '0000000163', '00000163', CAST(CAST(@now AS date) AS datetime), CAST(CAST(@now AS time(0)) AS datetime), 0, 0, 'KMS-TEST'),
       (1, '0000001113', '00001113', CAST(CAST(@now AS date) AS datetime), CAST(CAST(DATEADD(SECOND, 3, @now) AS time(0)) AS datetime), 0, 0, 'KMS-TEST');
PRINT CONCAT(CONVERT(varchar(8), @now, 108), '  3) Shahbaz (not authorized) took ASMF');
