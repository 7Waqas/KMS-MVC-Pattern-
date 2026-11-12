/* =====================================================================
   TEST — LIVE ALERT (laptop only).
   Run this while any KMS page is open in the browser (click the page
   once first so the browser allows sound).
   Within ~25 seconds a red alert pops up with a beep:
     "key_ RO(M&E)-1 scanned OUT with NO face"
   Run it again to get another alert (it uses the current time).
   ===================================================================== */
SET NOCOUNT ON;
DECLARE @now datetime = GETDATE();

IF EXISTS (SELECT 1 FROM dbo.tmpTRecords
           WHERE CAST(KqDate AS date) = CAST(@now AS date) AND ISNULL(sign_cause, '') <> 'KMS-TEST')
BEGIN
    RAISERROR('Real scans exist for today — this looks like a live database. Nothing inserted.', 16, 1);
    RETURN;
END

INSERT INTO dbo.tmpTRecords (clock_id, card_id, emp_id, KqDate, KqTime, mark, flag, sign_cause)
VALUES (1, '0000001099', '00001099',
        CAST(CAST(@now AS date) AS datetime),
        CAST(CAST(@now AS time(0)) AS datetime),
        0, 0, 'KMS-TEST');

PRINT CONCAT('Key RO(M&E)-1 scanned at ', CONVERT(varchar(8), @now, 108), ' with no face — watch KMS for the alert.');
