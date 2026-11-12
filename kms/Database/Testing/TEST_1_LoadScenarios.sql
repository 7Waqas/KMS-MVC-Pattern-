/* =====================================================================
   TEST DATA — LAPTOP ONLY. Never run on the office server.
   ---------------------------------------------------------------------
   Inserts fake device scans for YESTERDAY and TODAY so every Security
   screen can be tried. All rows are marked sign_cause = 'KMS-TEST' and
   are removed again by TEST_3_RemoveTestData.sql.

   Uses real keys/people from the local copy:
     key_D-1-1 (1060)       authorized: Faisal Naseer (91)
     key_D-11-1 (1022)      authorized: Babar Saeed (30)
     key_AGPR-1 (1110)      authorized: Aqal Mir (59) ...
     key_ASMF (1113)        authorized: Abdul Rauf (210), Muhammad Yasir (211)
     key_ AC(M&E)-1 (1098)  authorized: Muhammad Haroon (149)
     key_ RO(M&E)-1 (1099)  authorized: Mansoor Abbas (151)
     NOT authorized for any key: Nisar Hussain (115), Muhammad Hamza Ch (117), Shahbaz (163)

   WHAT YOU SHOULD SEE AFTER ~15 SECONDS (KMS running):
   YESTERDAY (a finished day → end-of-day checks run)
     Y1 key_D-1-1        normal: out 08:00 / back 16:30 by Faisal          → no incident
     Y2 key_ASMF         never scanned out, scanned IN 15:45 by Yasir     → TAKEN WITHOUT SCAN (Yasir #1, Nisar also at board)
     Y3 key_AC(M&E)-1    out 08:30 by Haroon, return never scanned         → RETURN NOT SCANNED (Haroon)
     Y4 key_RO(M&E)-1    Mansoor at the board 09:00, key never scanned     → SILENT KEY (low)
   TODAY (still running → shown live)
     T1 key_D-11-1       out 08:10 by Babar (authorized)                   → board: OUT with Babar
     T2 key_AGPR-1       out 08:40 with Hamza's face (NOT authorized)      → board: NOT AUTHORIZED + incident
     T3 key_D-1-1        out 09:05 with NO face; Shahbaz at board 09:06    → board: NO FACE + incident (Shahbaz suspect)
     T4 key_ASMF         out 08:15 / back 10:30 by Abdul Rauf              → board: RETURNED
   ===================================================================== */
SET NOCOUNT ON;
DECLARE @today date = CAST(GETDATE() AS date);
DECLARE @yest  date = DATEADD(DAY, -1, @today);

-- Safety: refuse if real scans exist on these days (i.e. this is not a test database)
IF EXISTS (SELECT 1 FROM dbo.tmpTRecords
           WHERE CAST(KqDate AS date) IN (@yest, @today) AND ISNULL(sign_cause, '') <> 'KMS-TEST')
BEGIN
    RAISERROR('Real scans exist for yesterday/today — this looks like a live database. Test data NOT inserted.', 16, 1);
    RETURN;
END

-- Clean previous test run first
DELETE FROM dbo.tmpTRecords WHERE sign_cause = 'KMS-TEST';

DECLARE @s TABLE (d date, t time(0), enroll int);
INSERT INTO @s (d, t, enroll) VALUES
 -- Y1 normal
 (@yest,  '08:00:00',   91), (@yest,  '08:00:06', 1060),
 (@yest,  '16:30:00',   91), (@yest,  '16:30:05', 1060),
 -- Y2 taken without scan (Nisar at the board in the morning, Yasir returns it)
 (@yest,  '08:20:00',  115),
 (@yest,  '15:45:00',  211), (@yest,  '15:45:04', 1113),
 -- Y3 return not scanned
 (@yest,  '08:30:00',  149), (@yest,  '08:30:05', 1098),
 -- Y4 silent key: Mansoor at the board, his key never scanned
 (@yest,  '09:00:00',  151),
 -- T1 normal out
 (@today, '08:10:00',   30), (@today, '08:10:05', 1022),
 -- T2 not authorized
 (@today, '08:40:00',  117), (@today, '08:40:04', 1110),
 -- T3 key without face (Shahbaz scanned 90 s later)
 (@today, '09:05:00', 1060), (@today, '09:06:30',  163),
 -- T4 out and returned
 (@today, '08:15:00',  210), (@today, '08:15:05', 1113),
 (@today, '10:30:00',  210), (@today, '10:30:06', 1113);

INSERT INTO dbo.tmpTRecords (clock_id, card_id, emp_id, KqDate, KqTime, mark, flag, sign_cause)
SELECT 1,
       RIGHT('0000000000' + CAST(enroll AS varchar(10)), 10),
       RIGHT('00000000'   + CAST(enroll AS varchar(8)), 8),
       CAST(d AS datetime),
       CAST(t AS datetime),          -- 1900-01-01 + time, same as the device software
       0, 0, 'KMS-TEST'
FROM @s
ORDER BY d, t;

PRINT CONCAT('Inserted ', @@ROWCOUNT, ' test scans for ', CONVERT(varchar(10), @yest, 120), ' and ', CONVERT(varchar(10), @today, 120), '.');
PRINT 'Keep KMS running; results appear within ~15 seconds. Open Security > Live Key Board.';
