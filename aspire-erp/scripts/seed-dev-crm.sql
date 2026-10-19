-- =============================================================================
-- Dev-only seed: CRM parity catalogs (loss reasons, territories, industries)
--
-- DEVELOPMENT ONLY. Deterministic GUIDs so integration tests and manual sessions
-- can reference fixed ids:
--   Loss reasons : c1000000-0000-4000-8000-0000000000<nn>
--   Territories  : c2000000-0000-4000-8000-0000000000<nn>
--   Industries   : c3000000-0000-4000-8000-0000000000<nn>
--
-- The API self-seeds these per tenant on first read (CrmRepository catalog ensure),
-- so this script only fast-forwards the dev tenant with the SAME code vocabulary
-- (re-runs and the ensure path converge: identical codes, surrogate ids may differ).
-- Idempotent: every statement is guarded, safe to re-run.
-- =============================================================================
USE [erp-db];
GO

-- --- Loss reasons -----------------------------------------------------------
DECLARE @Loss TABLE (Id UNIQUEIDENTIFIER, Code NVARCHAR(50), Name NVARCHAR(150));
INSERT INTO @Loss (Id, Code, Name) VALUES
    ('c1000000-0000-4000-8000-000000000001', 'PRICE', 'Lost on Price'),
    ('c1000000-0000-4000-8000-000000000002', 'COMPETITOR', 'Lost to Competitor'),
    ('c1000000-0000-4000-8000-000000000003', 'TIMING', 'Bad Timing'),
    ('c1000000-0000-4000-8000-000000000004', 'BUDGET', 'No Budget'),
    ('c1000000-0000-4000-8000-000000000005', 'FEATURE', 'Missing Features'),
    ('c1000000-0000-4000-8000-000000000006', 'RELATIONSHIP', 'Incumbent Relationship'),
    ('c1000000-0000-4000-8000-000000000007', 'OTHER', 'Other');

MERGE INTO dbo.OpportunityLossReason AS target
USING (SELECT '11111111-1111-4111-8111-111111111111' AS TenantId, Id, Code, Name FROM @Loss) AS source
    ON target.TenantId = source.TenantId AND target.Code = source.Code
WHEN NOT MATCHED THEN
    INSERT (Id, TenantId, Code, Name, IsActive)
    VALUES (source.Id, source.TenantId, source.Code, source.Name, 1);
GO

-- --- Territories ------------------------------------------------------------
DECLARE @Territory TABLE (Id UNIQUEIDENTIFIER, Code NVARCHAR(50), Name NVARCHAR(150));
INSERT INTO @Territory (Id, Code, Name) VALUES
    ('c2000000-0000-4000-8000-000000000001', 'NORTH', 'North'),
    ('c2000000-0000-4000-8000-000000000002', 'SOUTH', 'South'),
    ('c2000000-0000-4000-8000-000000000003', 'EAST', 'East'),
    ('c2000000-0000-4000-8000-000000000004', 'WEST', 'West'),
    ('c2000000-0000-4000-8000-000000000005', 'CENTRAL', 'Central'),
    ('c2000000-0000-4000-8000-000000000006', 'INTL', 'International');

MERGE INTO dbo.Territory AS target
USING (SELECT '11111111-1111-4111-8111-111111111111' AS TenantId, Id, Code, Name FROM @Territory) AS source
    ON target.TenantId = source.TenantId AND target.Code = source.Code
WHEN NOT MATCHED THEN
    INSERT (Id, TenantId, Code, Name, IsActive)
    VALUES (source.Id, source.TenantId, source.Code, source.Name, 1);
GO

-- --- Industries -------------------------------------------------------------
DECLARE @Industry TABLE (Id UNIQUEIDENTIFIER, Code NVARCHAR(50), Name NVARCHAR(150));
INSERT INTO @Industry (Id, Code, Name) VALUES
    ('c3000000-0000-4000-8000-000000000001', 'MANUFACTURING', 'Manufacturing'),
    ('c3000000-0000-4000-8000-000000000002', 'RETAIL', 'Retail'),
    ('c3000000-0000-4000-8000-000000000003', 'HEALTHCARE', 'Healthcare'),
    ('c3000000-0000-4000-8000-000000000004', 'TECHNOLOGY', 'Technology'),
    ('c3000000-0000-4000-8000-000000000005', 'SERVICES', 'Services'),
    ('c3000000-0000-4000-8000-000000000006', 'CONSTRUCTION', 'Construction'),
    ('c3000000-0000-4000-8000-000000000007', 'OTHER', 'Other');

MERGE INTO dbo.Industry AS target
USING (SELECT '11111111-1111-4111-8111-111111111111' AS TenantId, Id, Code, Name FROM @Industry) AS source
    ON target.TenantId = source.TenantId AND target.Code = source.Code
WHEN NOT MATCHED THEN
    INSERT (Id, TenantId, Code, Name, IsActive)
    VALUES (source.Id, source.TenantId, source.Code, source.Name, 1);
GO

-- Verification
SELECT 'LossReason' AS Catalog, COUNT(*) AS Rows FROM dbo.OpportunityLossReason
WHERE TenantId = '11111111-1111-4111-8111-111111111111'
UNION ALL
SELECT 'Territory', COUNT(*) FROM dbo.Territory
WHERE TenantId = '11111111-1111-4111-8111-111111111111'
UNION ALL
SELECT 'Industry', COUNT(*) FROM dbo.Industry
WHERE TenantId = '11111111-1111-4111-8111-111111111111';
GO
