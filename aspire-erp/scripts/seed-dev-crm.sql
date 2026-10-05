-- =============================================================================
-- Dev-only seed: CRM & Sales Pipeline (module 08-crm, task 11.7)
--
-- DEVELOPMENT ONLY. Deterministic GUIDs so the frontend .env.development and
-- manual curl sessions can reference fixed ids:
--   Tenant      : 11111111-1111-4111-8111-111111111111 (scripts/seed-dev-coa.sql)
--   Company     : 22222222-2222-4222-8222-222222222222 (scripts/seed-dev-coa.sql)
--   Demo lead   : c0000000-0000-4000-8000-000000000001 (LEAD-DEMO-001, Alex Rivera)
--   Opportunity : c0000000-0000-4000-8000-000000000002 (OPP-2026-09901, Qualification)
--   Activity    : c0000000-0000-4000-8000-000000000003 (conversion note on the demo deal)
--
-- Lead/Opportunity are system-versioned (temporal): PeriodStart/PeriodEnd are
-- GENERATED ALWAYS columns and must NOT be listed on INSERT (banking seed precedent).
-- The demo opportunity number uses the 09901 sequence band so live webhook/convert
-- numbering (OPP-YYYY-00001 upward) never collides with it.
-- Idempotent: every statement is guarded, safe to re-run.
--
-- TEST HERMITICITY RULE: integration tests use their OWN unique rows
-- (CRM-IT- prefix) and never read or assert on these seed rows.
-- =============================================================================
USE [erp-db];
GO

-- --- Demo lead ---------------------------------------------------------------
-- Alex Rivera at TechCorp, the spec CRM-01 inbound persona, still Open so the dev
-- Kanban/convert flow can be clicked through by hand.
IF NOT EXISTS (SELECT 1 FROM dbo.Lead WHERE Id = 'c0000000-0000-4000-8000-000000000001')
BEGIN
    INSERT INTO dbo.Lead (Id, TenantId, CompanyId, LeadCode, LeadName, OrganizationName, Email, Phone, Source, Status, AssignedToUserId, ConvertedOpportunityId, ConvertedCustomerId, IsActive, ExternalReference)
    VALUES ('c0000000-0000-4000-8000-000000000001',
            '11111111-1111-4111-8111-111111111111',
            '22222222-2222-4222-8222-222222222222',
            'LEAD-DEMO-001', 'Alex Rivera', 'TechCorp',
            'alex.rivera@techcorp.example', '+1-555-0100', 'Website', 'Open',
            NULL, NULL, NULL, 1, 'demo-web-2026-001');
END;
GO

-- --- Demo opportunity ----------------------------------------------------------
-- Open deal from the demo lead at Qualification/25% ($15,000 -> $3,750 weighted,
-- the spec CRM-01 arithmetic) so the dev pipeline board is non-empty.
IF NOT EXISTS (SELECT 1 FROM dbo.Opportunity WHERE Id = 'c0000000-0000-4000-8000-000000000002')
BEGIN
    INSERT INTO dbo.Opportunity (Id, TenantId, CompanyId, OpportunityNumber, OpportunityFrom, PartyId, PartyName, Stage, OpportunityAmount, Probability, Currency, ExpectedClosingDate, Status, LossReason, AssignedSalespersonId)
    VALUES ('c0000000-0000-4000-8000-000000000002',
            '11111111-1111-4111-8111-111111111111',
            '22222222-2222-4222-8222-222222222222',
            'OPP-2026-09901', 'Lead', 'c0000000-0000-4000-8000-000000000001', 'TechCorp',
            'Qualification', 15000.00, 25.00, 'USD', '2027-06-30', 'Open', NULL, NULL);
END;
GO

-- --- Demo activity ---------------------------------------------------------------
-- Conversion-style note on the demo deal (spec CRM-03 audit trail shape).
IF NOT EXISTS (SELECT 1 FROM dbo.CRMActivity WHERE Id = 'c0000000-0000-4000-8000-000000000003')
BEGIN
    INSERT INTO dbo.CRMActivity (Id, OpportunityId, Type, Subject, Content, ActivityDate, NextFollowUpDate, CreatedByUserId)
    VALUES ('c0000000-0000-4000-8000-000000000003',
            'c0000000-0000-4000-8000-000000000002', 'Note',
            'Converted from lead LEAD-DEMO-001',
            'Converted from lead LEAD-DEMO-001 (Alex Rivera) at TechCorp.',
            SYSDATETIMEOFFSET(), NULL,
            '33333333-3333-4333-8333-333333333333');
END;
GO
