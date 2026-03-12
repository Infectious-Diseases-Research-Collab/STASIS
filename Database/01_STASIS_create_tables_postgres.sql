--
-- PostgreSQL database dump
--

\restrict x18mcdOchBy4Z81yyg8g9Ir0EwJAJUb9ovgZ6OSNCJahXZrYjL9YbLdTYQV8xpu

-- Dumped from database version 17.9 (Homebrew)
-- Dumped by pg_dump version 17.9 (Homebrew)

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

ALTER TABLE IF EXISTS ONLY public."tbl_UserProfiles" DROP CONSTRAINT IF EXISTS "FK_UserProfiles_AspNetUsers";
ALTER TABLE IF EXISTS ONLY public."tbl_Specimens" DROP CONSTRAINT IF EXISTS "FK_Specimens_Studies";
ALTER TABLE IF EXISTS ONLY public."tbl_Specimens" DROP CONSTRAINT IF EXISTS "FK_Specimens_SampleTypes";
ALTER TABLE IF EXISTS ONLY public."tbl_Specimens" DROP CONSTRAINT IF EXISTS "FK_Specimens_DiscardApproval";
ALTER TABLE IF EXISTS ONLY public."tbl_Specimens" DROP CONSTRAINT IF EXISTS "FK_Specimens_Boxes";
ALTER TABLE IF EXISTS ONLY public."tbl_Shipments" DROP CONSTRAINT IF EXISTS "FK_Shipments_ShippedBy";
ALTER TABLE IF EXISTS ONLY public."tbl_Shipments" DROP CONSTRAINT IF EXISTS "FK_Shipments_ShippedBox";
ALTER TABLE IF EXISTS ONLY public."tbl_Shipments" DROP CONSTRAINT IF EXISTS "FK_Shipments_Batch";
ALTER TABLE IF EXISTS ONLY public."tbl_ShipmentRequests" DROP CONSTRAINT IF EXISTS "FK_ShipmentRequests_Specimen";
ALTER TABLE IF EXISTS ONLY public."tbl_ShipmentRequests" DROP CONSTRAINT IF EXISTS "FK_ShipmentRequests_SampleType";
ALTER TABLE IF EXISTS ONLY public."tbl_ShipmentRequests" DROP CONSTRAINT IF EXISTS "FK_ShipmentRequests_Batch";
ALTER TABLE IF EXISTS ONLY public."tbl_ShipmentContents" DROP CONSTRAINT IF EXISTS "FK_ShipmentContents_Specimen";
ALTER TABLE IF EXISTS ONLY public."tbl_ShipmentContents" DROP CONSTRAINT IF EXISTS "FK_ShipmentContents_Shipment";
ALTER TABLE IF EXISTS ONLY public."tbl_ShipmentBatches" DROP CONSTRAINT IF EXISTS "FK_ShipmentBatches_ImportedBy";
ALTER TABLE IF EXISTS ONLY public."tbl_ShipmentBatches" DROP CONSTRAINT IF EXISTS "FK_ShipmentBatches_Approval";
ALTER TABLE IF EXISTS ONLY public."tbl_Racks" DROP CONSTRAINT IF EXISTS "FK_Racks_Freezers";
ALTER TABLE IF EXISTS ONLY public."tbl_FilterPaperUsages" DROP CONSTRAINT IF EXISTS "FK_FilterPaperUsages_UsedBy";
ALTER TABLE IF EXISTS ONLY public."tbl_FilterPaperUsages" DROP CONSTRAINT IF EXISTS "FK_FilterPaperUsages_Specimens";
ALTER TABLE IF EXISTS ONLY public."tbl_FilterPaperUsages" DROP CONSTRAINT IF EXISTS "FK_FilterPaperUsages_ShipmentContent";
ALTER TABLE IF EXISTS ONLY public."tbl_Boxes" DROP CONSTRAINT IF EXISTS "FK_Boxes_Racks";
ALTER TABLE IF EXISTS ONLY public."tbl_AuditLog" DROP CONSTRAINT IF EXISTS "FK_AuditLog_ChangedBy";
ALTER TABLE IF EXISTS ONLY public."AspNetUserTokens" DROP CONSTRAINT IF EXISTS "FK_AspNetUserTokens_AspNetUsers_UserId";
ALTER TABLE IF EXISTS ONLY public."AspNetUserRoles" DROP CONSTRAINT IF EXISTS "FK_AspNetUserRoles_AspNetUsers_UserId";
ALTER TABLE IF EXISTS ONLY public."AspNetUserRoles" DROP CONSTRAINT IF EXISTS "FK_AspNetUserRoles_AspNetRoles_RoleId";
ALTER TABLE IF EXISTS ONLY public."AspNetUserLogins" DROP CONSTRAINT IF EXISTS "FK_AspNetUserLogins_AspNetUsers_UserId";
ALTER TABLE IF EXISTS ONLY public."AspNetUserClaims" DROP CONSTRAINT IF EXISTS "FK_AspNetUserClaims_AspNetUsers_UserId";
ALTER TABLE IF EXISTS ONLY public."AspNetRoleClaims" DROP CONSTRAINT IF EXISTS "FK_AspNetRoleClaims_AspNetRoles_RoleId";
ALTER TABLE IF EXISTS ONLY public."tbl_Approvals" DROP CONSTRAINT IF EXISTS "FK_Approvals_RequestedBy";
ALTER TABLE IF EXISTS ONLY public."tbl_Approvals" DROP CONSTRAINT IF EXISTS "FK_Approvals_RegulatoryApprover";
ALTER TABLE IF EXISTS ONLY public."tbl_Approvals" DROP CONSTRAINT IF EXISTS "FK_Approvals_PIApprover";
ALTER TABLE IF EXISTS ONLY public."tbl_Approvals" DROP CONSTRAINT IF EXISTS "FK_Approvals_EDApprover";
DROP INDEX IF EXISTS public."UserNameIndex";
DROP INDEX IF EXISTS public."RoleNameIndex";
DROP INDEX IF EXISTS public."IX_AuditLog_Timestamp";
DROP INDEX IF EXISTS public."IX_AuditLog_TableRecord";
DROP INDEX IF EXISTS public."IX_AspNetUserRoles_RoleId";
DROP INDEX IF EXISTS public."IX_AspNetUserLogins_UserId";
DROP INDEX IF EXISTS public."IX_AspNetUserClaims_UserId";
DROP INDEX IF EXISTS public."IX_AspNetRoleClaims_RoleId";
DROP INDEX IF EXISTS public."EmailIndex";
ALTER TABLE IF EXISTS ONLY public."tbl_UserProfiles" DROP CONSTRAINT IF EXISTS "UQ_UserProfiles_AspNetUserId";
ALTER TABLE IF EXISTS ONLY public."tbl_Studies" DROP CONSTRAINT IF EXISTS "UQ_Studies_StudyCode";
ALTER TABLE IF EXISTS ONLY public."tbl_Specimens" DROP CONSTRAINT IF EXISTS "UQ_Specimens_BoxPosition";
ALTER TABLE IF EXISTS ONLY public."tbl_Specimens" DROP CONSTRAINT IF EXISTS "UQ_Specimens_BarcodeID";
ALTER TABLE IF EXISTS ONLY public."tbl_SampleTypes" DROP CONSTRAINT IF EXISTS "UQ_SampleTypes_TypeName";
ALTER TABLE IF EXISTS ONLY public."tbl_Freezers" DROP CONSTRAINT IF EXISTS "UQ_Freezers_FreezerName";
ALTER TABLE IF EXISTS ONLY public."tbl_Boxes" DROP CONSTRAINT IF EXISTS "UQ_Boxes_BoxLabel";
ALTER TABLE IF EXISTS ONLY public."__EFMigrationsHistory" DROP CONSTRAINT IF EXISTS "PK___EFMigrationsHistory";
ALTER TABLE IF EXISTS ONLY public."tbl_UserProfiles" DROP CONSTRAINT IF EXISTS "PK_UserProfiles";
ALTER TABLE IF EXISTS ONLY public."tbl_Studies" DROP CONSTRAINT IF EXISTS "PK_Studies";
ALTER TABLE IF EXISTS ONLY public."tbl_Specimens" DROP CONSTRAINT IF EXISTS "PK_Specimens";
ALTER TABLE IF EXISTS ONLY public."tbl_Shipments" DROP CONSTRAINT IF EXISTS "PK_Shipments";
ALTER TABLE IF EXISTS ONLY public."tbl_ShipmentRequests" DROP CONSTRAINT IF EXISTS "PK_ShipmentRequests";
ALTER TABLE IF EXISTS ONLY public."tbl_ShipmentContents" DROP CONSTRAINT IF EXISTS "PK_ShipmentContents";
ALTER TABLE IF EXISTS ONLY public."tbl_ShipmentBatches" DROP CONSTRAINT IF EXISTS "PK_ShipmentBatches";
ALTER TABLE IF EXISTS ONLY public."tbl_SampleTypes" DROP CONSTRAINT IF EXISTS "PK_SampleTypes";
ALTER TABLE IF EXISTS ONLY public."tbl_Racks" DROP CONSTRAINT IF EXISTS "PK_Racks";
ALTER TABLE IF EXISTS ONLY public."tbl_Freezers" DROP CONSTRAINT IF EXISTS "PK_Freezers";
ALTER TABLE IF EXISTS ONLY public."tbl_FilterPaperUsages" DROP CONSTRAINT IF EXISTS "PK_FilterPaperUsages";
ALTER TABLE IF EXISTS ONLY public."tbl_Boxes" DROP CONSTRAINT IF EXISTS "PK_Boxes";
ALTER TABLE IF EXISTS ONLY public."tbl_AuditLog" DROP CONSTRAINT IF EXISTS "PK_AuditLog";
ALTER TABLE IF EXISTS ONLY public."AspNetUsers" DROP CONSTRAINT IF EXISTS "PK_AspNetUsers";
ALTER TABLE IF EXISTS ONLY public."AspNetUserTokens" DROP CONSTRAINT IF EXISTS "PK_AspNetUserTokens";
ALTER TABLE IF EXISTS ONLY public."AspNetUserRoles" DROP CONSTRAINT IF EXISTS "PK_AspNetUserRoles";
ALTER TABLE IF EXISTS ONLY public."AspNetUserLogins" DROP CONSTRAINT IF EXISTS "PK_AspNetUserLogins";
ALTER TABLE IF EXISTS ONLY public."AspNetUserClaims" DROP CONSTRAINT IF EXISTS "PK_AspNetUserClaims";
ALTER TABLE IF EXISTS ONLY public."AspNetRoles" DROP CONSTRAINT IF EXISTS "PK_AspNetRoles";
ALTER TABLE IF EXISTS ONLY public."AspNetRoleClaims" DROP CONSTRAINT IF EXISTS "PK_AspNetRoleClaims";
ALTER TABLE IF EXISTS ONLY public."tbl_Approvals" DROP CONSTRAINT IF EXISTS "PK_Approvals";
DROP TABLE IF EXISTS public."tbl_UserProfiles";
DROP TABLE IF EXISTS public."tbl_Studies";
DROP TABLE IF EXISTS public."tbl_Specimens";
DROP TABLE IF EXISTS public."tbl_Shipments";
DROP TABLE IF EXISTS public."tbl_ShipmentRequests";
DROP TABLE IF EXISTS public."tbl_ShipmentContents";
DROP TABLE IF EXISTS public."tbl_ShipmentBatches";
DROP TABLE IF EXISTS public."tbl_SampleTypes";
DROP TABLE IF EXISTS public."tbl_Racks";
DROP TABLE IF EXISTS public."tbl_Freezers";
DROP TABLE IF EXISTS public."tbl_FilterPaperUsages";
DROP TABLE IF EXISTS public."tbl_Boxes";
DROP TABLE IF EXISTS public."tbl_AuditLog";
DROP TABLE IF EXISTS public."tbl_Approvals";
DROP TABLE IF EXISTS public."__EFMigrationsHistory";
DROP TABLE IF EXISTS public."AspNetUsers";
DROP TABLE IF EXISTS public."AspNetUserTokens";
DROP TABLE IF EXISTS public."AspNetUserRoles";
DROP TABLE IF EXISTS public."AspNetUserLogins";
DROP TABLE IF EXISTS public."AspNetUserClaims";
DROP TABLE IF EXISTS public."AspNetRoles";
DROP TABLE IF EXISTS public."AspNetRoleClaims";
SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: AspNetRoleClaims; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AspNetRoleClaims" (
    "Id" integer NOT NULL,
    "RoleId" character varying(450) NOT NULL,
    "ClaimType" text,
    "ClaimValue" text
);


--
-- Name: AspNetRoleClaims_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."AspNetRoleClaims" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."AspNetRoleClaims_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: AspNetRoles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AspNetRoles" (
    "Id" character varying(450) NOT NULL,
    "Name" character varying(256),
    "NormalizedName" character varying(256),
    "ConcurrencyStamp" text
);


--
-- Name: AspNetUserClaims; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AspNetUserClaims" (
    "Id" integer NOT NULL,
    "UserId" character varying(450) NOT NULL,
    "ClaimType" text,
    "ClaimValue" text
);


--
-- Name: AspNetUserClaims_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."AspNetUserClaims" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."AspNetUserClaims_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: AspNetUserLogins; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AspNetUserLogins" (
    "LoginProvider" character varying(450) NOT NULL,
    "ProviderKey" character varying(450) NOT NULL,
    "ProviderDisplayName" text,
    "UserId" character varying(450) NOT NULL
);


--
-- Name: AspNetUserRoles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AspNetUserRoles" (
    "UserId" character varying(450) NOT NULL,
    "RoleId" character varying(450) NOT NULL
);


--
-- Name: AspNetUserTokens; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AspNetUserTokens" (
    "UserId" character varying(450) NOT NULL,
    "LoginProvider" character varying(450) NOT NULL,
    "Name" character varying(450) NOT NULL,
    "Value" text
);


--
-- Name: AspNetUsers; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AspNetUsers" (
    "Id" character varying(450) NOT NULL,
    "UserName" character varying(256),
    "NormalizedUserName" character varying(256),
    "Email" character varying(256),
    "NormalizedEmail" character varying(256),
    "EmailConfirmed" boolean DEFAULT false NOT NULL,
    "PasswordHash" text,
    "SecurityStamp" text,
    "ConcurrencyStamp" text,
    "PhoneNumber" text,
    "PhoneNumberConfirmed" boolean DEFAULT false NOT NULL,
    "TwoFactorEnabled" boolean DEFAULT false NOT NULL,
    "LockoutEnd" timestamp with time zone,
    "LockoutEnabled" boolean DEFAULT false NOT NULL,
    "AccessFailedCount" integer DEFAULT 0 NOT NULL
);


--
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL
);


--
-- Name: tbl_Approvals; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."tbl_Approvals" (
    "ApprovalID" integer NOT NULL,
    "ApprovalType" character varying(50) DEFAULT 'Shipment'::character varying NOT NULL,
    "RequestedByUserId" character varying(450),
    "RequestedDate" timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "EDApproverUserId" character varying(450),
    "EDApprovalDate" timestamp without time zone,
    "EDApprovalStatus" character varying(20),
    "EDComments" character varying(500),
    "RegulatoryApproverUserId" character varying(450),
    "RegulatoryApprovalDate" timestamp without time zone,
    "RegulatoryApprovalStatus" character varying(20),
    "RegulatoryComments" character varying(500),
    "PIApproverUserId" character varying(450),
    "PIApprovalDate" timestamp without time zone,
    "PIApprovalStatus" character varying(20),
    "PIComments" character varying(500),
    "OverallStatus" character varying(20) DEFAULT 'Pending'::character varying NOT NULL,
    CONSTRAINT "CK_Approvals_ApprovalType" CHECK ((("ApprovalType")::text = ANY ((ARRAY['Shipment'::character varying, 'Discard'::character varying, 'SingleAliquotException'::character varying])::text[]))),
    CONSTRAINT "CK_Approvals_EDApprovalStatus" CHECK ((("EDApprovalStatus" IS NULL) OR (("EDApprovalStatus")::text = ANY ((ARRAY['Pending'::character varying, 'Approved'::character varying, 'Rejected'::character varying])::text[])))),
    CONSTRAINT "CK_Approvals_OverallStatus" CHECK ((("OverallStatus")::text = ANY ((ARRAY['Pending'::character varying, 'Approved'::character varying, 'Rejected'::character varying])::text[]))),
    CONSTRAINT "CK_Approvals_PIApprovalStatus" CHECK ((("PIApprovalStatus" IS NULL) OR (("PIApprovalStatus")::text = ANY ((ARRAY['Pending'::character varying, 'Approved'::character varying, 'Rejected'::character varying])::text[])))),
    CONSTRAINT "CK_Approvals_RegulatoryApprovalStatus" CHECK ((("RegulatoryApprovalStatus" IS NULL) OR (("RegulatoryApprovalStatus")::text = ANY ((ARRAY['Pending'::character varying, 'Approved'::character varying, 'Rejected'::character varying])::text[]))))
);


--
-- Name: tbl_Approvals_ApprovalID_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."tbl_Approvals" ALTER COLUMN "ApprovalID" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."tbl_Approvals_ApprovalID_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tbl_AuditLog; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."tbl_AuditLog" (
    "AuditLogID" integer NOT NULL,
    "TableName" character varying(100) NOT NULL,
    "RecordID" integer NOT NULL,
    "FieldName" character varying(100) NOT NULL,
    "OldValue" text,
    "NewValue" text,
    "ChangedByUserId" character varying(450),
    "Timestamp" timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


--
-- Name: tbl_AuditLog_AuditLogID_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."tbl_AuditLog" ALTER COLUMN "AuditLogID" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."tbl_AuditLog_AuditLogID_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tbl_Boxes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."tbl_Boxes" (
    "BoxID" integer NOT NULL,
    "BoxLabel" character varying(50) NOT NULL,
    "BoxType" character varying(50) NOT NULL,
    "BoxCategory" character varying(50) DEFAULT 'Standard'::character varying NOT NULL,
    "RackID" integer,
    CONSTRAINT "CK_Boxes_BoxCategory" CHECK ((("BoxCategory")::text = ANY ((ARRAY['Standard'::character varying, 'Temp'::character varying, 'Trash'::character varying, 'Shipping'::character varying])::text[]))),
    CONSTRAINT "CK_Boxes_BoxType" CHECK ((("BoxType")::text = ANY ((ARRAY['81-slot'::character varying, '100-slot'::character varying, 'Filter Paper Binder'::character varying])::text[])))
);


--
-- Name: tbl_Boxes_BoxID_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."tbl_Boxes" ALTER COLUMN "BoxID" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."tbl_Boxes_BoxID_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tbl_FilterPaperUsages; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."tbl_FilterPaperUsages" (
    "FilterPaperUsageID" integer NOT NULL,
    "SpecimenID" integer NOT NULL,
    "UsageDate" timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "SpotsUsed" integer NOT NULL,
    "IsInternationalShipment" boolean DEFAULT false NOT NULL,
    "UsedByUserId" character varying(450),
    "ShipmentContentID" integer,
    "Notes" text,
    CONSTRAINT "CK_FilterPaperUsages_SpotsUsed" CHECK ((("SpotsUsed" >= 1) AND ("SpotsUsed" <= 4)))
);


--
-- Name: tbl_FilterPaperUsages_FilterPaperUsageID_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."tbl_FilterPaperUsages" ALTER COLUMN "FilterPaperUsageID" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."tbl_FilterPaperUsages_FilterPaperUsageID_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tbl_Freezers; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."tbl_Freezers" (
    "FreezerID" integer NOT NULL,
    "FreezerName" character varying(50) NOT NULL,
    "Temperature" integer,
    "LocationInBuilding" character varying(100)
);


--
-- Name: tbl_Freezers_FreezerID_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."tbl_Freezers" ALTER COLUMN "FreezerID" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."tbl_Freezers_FreezerID_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tbl_Racks; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."tbl_Racks" (
    "RackID" integer NOT NULL,
    "RackName" character varying(50) NOT NULL,
    "FreezerID" integer
);


--
-- Name: tbl_Racks_RackID_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."tbl_Racks" ALTER COLUMN "RackID" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."tbl_Racks_RackID_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tbl_SampleTypes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."tbl_SampleTypes" (
    "SampleTypeID" integer NOT NULL,
    "TypeName" character varying(50) NOT NULL,
    "IsConsumable" boolean DEFAULT false NOT NULL,
    "MaxShippableUnits" integer,
    "LocalReserveUnits" integer
);


--
-- Name: tbl_SampleTypes_SampleTypeID_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."tbl_SampleTypes" ALTER COLUMN "SampleTypeID" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."tbl_SampleTypes_SampleTypeID_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tbl_ShipmentBatches; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."tbl_ShipmentBatches" (
    "BatchID" integer NOT NULL,
    "ImportDate" timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "ImportedByUserId" character varying(450),
    "RequestorName" character varying(100),
    "RequestorEmail" character varying(100),
    "TotalRequested" integer DEFAULT 0 NOT NULL,
    "TotalAvailable" integer DEFAULT 0 NOT NULL,
    "TotalNotFound" integer DEFAULT 0 NOT NULL,
    "TotalPreviouslyShipped" integer DEFAULT 0 NOT NULL,
    "TotalDiscarded" integer DEFAULT 0 NOT NULL,
    "TotalNotYetReceived" integer DEFAULT 0 NOT NULL,
    "ApprovalID" integer,
    "Status" character varying(50) DEFAULT 'Pending Approval'::character varying NOT NULL,
    CONSTRAINT "CK_ShipmentBatches_Status" CHECK ((("Status")::text = ANY ((ARRAY['Pending Approval'::character varying, 'Approved'::character varying, 'Rejected'::character varying, 'Processing'::character varying, 'Shipped'::character varying])::text[])))
);


--
-- Name: tbl_ShipmentBatches_BatchID_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."tbl_ShipmentBatches" ALTER COLUMN "BatchID" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."tbl_ShipmentBatches_BatchID_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tbl_ShipmentContents; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."tbl_ShipmentContents" (
    "ShipmentContentID" integer NOT NULL,
    "ShipmentID" integer NOT NULL,
    "SpecimenID" integer NOT NULL,
    "ConditionAtShipment" character varying(100),
    "ShippingBoxPosition" character varying(20),
    "SpotsUsed" integer
);


--
-- Name: tbl_ShipmentContents_ShipmentContentID_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."tbl_ShipmentContents" ALTER COLUMN "ShipmentContentID" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."tbl_ShipmentContents_ShipmentContentID_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tbl_ShipmentRequests; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."tbl_ShipmentRequests" (
    "RequestID" integer NOT NULL,
    "BatchID" integer,
    "RequestedBarcode" character varying(100) NOT NULL,
    "RequestedSampleTypeID" integer,
    "RequestorName" character varying(100),
    "RequestDate" timestamp without time zone,
    "MatchedSpecimenID" integer,
    "Status" character varying(50) DEFAULT 'Pending'::character varying NOT NULL,
    CONSTRAINT "CK_ShipmentRequests_Status" CHECK ((("Status")::text = ANY ((ARRAY['Pending'::character varying, 'Staged'::character varying, 'Shipped'::character varying, 'Not Found'::character varying, 'Previously Shipped'::character varying, 'Discarded'::character varying, 'Not Yet Received'::character varying])::text[])))
);


--
-- Name: tbl_ShipmentRequests_RequestID_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."tbl_ShipmentRequests" ALTER COLUMN "RequestID" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."tbl_ShipmentRequests_RequestID_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tbl_Shipments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."tbl_Shipments" (
    "ShipmentID" integer NOT NULL,
    "BatchID" integer,
    "ShipmentDate" timestamp without time zone NOT NULL,
    "Courier" character varying(100),
    "TrackingNumber" character varying(100),
    "DestinationAddress" character varying(255),
    "ShippedByUserId" character varying(450),
    "IsEntireBox" boolean DEFAULT false NOT NULL,
    "ShippedBoxID" integer
);


--
-- Name: tbl_Shipments_ShipmentID_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."tbl_Shipments" ALTER COLUMN "ShipmentID" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."tbl_Shipments_ShipmentID_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tbl_Specimens; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."tbl_Specimens" (
    "SpecimenID" integer NOT NULL,
    "BarcodeID" character varying(100) NOT NULL,
    "LegacyID" character varying(100),
    "StudyID" integer,
    "SampleTypeID" integer,
    "CollectionDate" timestamp without time zone,
    "BoxID" integer,
    "PositionRow" integer,
    "PositionCol" integer,
    "RemainingSpots" integer,
    "SpotsShippedInternational" integer DEFAULT 0 NOT NULL,
    "SpotsReservedLocal" integer DEFAULT 0 NOT NULL,
    "AliquotNumber" integer,
    "DiscardApprovalID" integer,
    "Status" character varying(50) DEFAULT 'In-Stock'::character varying NOT NULL,
    CONSTRAINT "CK_Specimens_AliquotNumber" CHECK ((("AliquotNumber" IS NULL) OR ("AliquotNumber" = ANY (ARRAY[1, 2])))),
    CONSTRAINT "CK_Specimens_Status" CHECK ((("Status")::text = ANY ((ARRAY['In-Stock'::character varying, 'Staged'::character varying, 'Shipped'::character varying, 'Missing'::character varying, 'Depleted'::character varying, 'Discarded'::character varying, 'Temp'::character varying, 'Not Yet Received'::character varying])::text[])))
);


--
-- Name: tbl_Specimens_SpecimenID_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."tbl_Specimens" ALTER COLUMN "SpecimenID" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."tbl_Specimens_SpecimenID_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tbl_Studies; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."tbl_Studies" (
    "StudyID" integer NOT NULL,
    "StudyCode" character varying(50) NOT NULL,
    "StudyName" character varying(200),
    "PrincipalInvestigator" character varying(100)
);


--
-- Name: tbl_Studies_StudyID_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."tbl_Studies" ALTER COLUMN "StudyID" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."tbl_Studies_StudyID_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tbl_UserProfiles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."tbl_UserProfiles" (
    "UserProfileID" integer NOT NULL,
    "AspNetUserId" character varying(450) NOT NULL,
    "Department" character varying(100),
    "CanApproveShipments" boolean DEFAULT false NOT NULL,
    "CanApproveDiscards" boolean DEFAULT false NOT NULL,
    "MustChangePassword" boolean DEFAULT false NOT NULL
);


--
-- Name: tbl_UserProfiles_UserProfileID_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."tbl_UserProfiles" ALTER COLUMN "UserProfileID" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."tbl_UserProfiles_UserProfileID_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tbl_Approvals PK_Approvals; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Approvals"
    ADD CONSTRAINT "PK_Approvals" PRIMARY KEY ("ApprovalID");


--
-- Name: AspNetRoleClaims PK_AspNetRoleClaims; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetRoleClaims"
    ADD CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY ("Id");


--
-- Name: AspNetRoles PK_AspNetRoles; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetRoles"
    ADD CONSTRAINT "PK_AspNetRoles" PRIMARY KEY ("Id");


--
-- Name: AspNetUserClaims PK_AspNetUserClaims; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetUserClaims"
    ADD CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY ("Id");


--
-- Name: AspNetUserLogins PK_AspNetUserLogins; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetUserLogins"
    ADD CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey");


--
-- Name: AspNetUserRoles PK_AspNetUserRoles; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetUserRoles"
    ADD CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId");


--
-- Name: AspNetUserTokens PK_AspNetUserTokens; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetUserTokens"
    ADD CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name");


--
-- Name: AspNetUsers PK_AspNetUsers; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetUsers"
    ADD CONSTRAINT "PK_AspNetUsers" PRIMARY KEY ("Id");


--
-- Name: tbl_AuditLog PK_AuditLog; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_AuditLog"
    ADD CONSTRAINT "PK_AuditLog" PRIMARY KEY ("AuditLogID");


--
-- Name: tbl_Boxes PK_Boxes; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Boxes"
    ADD CONSTRAINT "PK_Boxes" PRIMARY KEY ("BoxID");


--
-- Name: tbl_FilterPaperUsages PK_FilterPaperUsages; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_FilterPaperUsages"
    ADD CONSTRAINT "PK_FilterPaperUsages" PRIMARY KEY ("FilterPaperUsageID");


--
-- Name: tbl_Freezers PK_Freezers; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Freezers"
    ADD CONSTRAINT "PK_Freezers" PRIMARY KEY ("FreezerID");


--
-- Name: tbl_Racks PK_Racks; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Racks"
    ADD CONSTRAINT "PK_Racks" PRIMARY KEY ("RackID");


--
-- Name: tbl_SampleTypes PK_SampleTypes; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_SampleTypes"
    ADD CONSTRAINT "PK_SampleTypes" PRIMARY KEY ("SampleTypeID");


--
-- Name: tbl_ShipmentBatches PK_ShipmentBatches; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_ShipmentBatches"
    ADD CONSTRAINT "PK_ShipmentBatches" PRIMARY KEY ("BatchID");


--
-- Name: tbl_ShipmentContents PK_ShipmentContents; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_ShipmentContents"
    ADD CONSTRAINT "PK_ShipmentContents" PRIMARY KEY ("ShipmentContentID");


--
-- Name: tbl_ShipmentRequests PK_ShipmentRequests; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_ShipmentRequests"
    ADD CONSTRAINT "PK_ShipmentRequests" PRIMARY KEY ("RequestID");


--
-- Name: tbl_Shipments PK_Shipments; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Shipments"
    ADD CONSTRAINT "PK_Shipments" PRIMARY KEY ("ShipmentID");


--
-- Name: tbl_Specimens PK_Specimens; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Specimens"
    ADD CONSTRAINT "PK_Specimens" PRIMARY KEY ("SpecimenID");


--
-- Name: tbl_Studies PK_Studies; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Studies"
    ADD CONSTRAINT "PK_Studies" PRIMARY KEY ("StudyID");


--
-- Name: tbl_UserProfiles PK_UserProfiles; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_UserProfiles"
    ADD CONSTRAINT "PK_UserProfiles" PRIMARY KEY ("UserProfileID");


--
-- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");


--
-- Name: tbl_Boxes UQ_Boxes_BoxLabel; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Boxes"
    ADD CONSTRAINT "UQ_Boxes_BoxLabel" UNIQUE ("BoxLabel");


--
-- Name: tbl_Freezers UQ_Freezers_FreezerName; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Freezers"
    ADD CONSTRAINT "UQ_Freezers_FreezerName" UNIQUE ("FreezerName");


--
-- Name: tbl_SampleTypes UQ_SampleTypes_TypeName; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_SampleTypes"
    ADD CONSTRAINT "UQ_SampleTypes_TypeName" UNIQUE ("TypeName");


--
-- Name: tbl_Specimens UQ_Specimens_BarcodeID; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Specimens"
    ADD CONSTRAINT "UQ_Specimens_BarcodeID" UNIQUE ("BarcodeID");


--
-- Name: tbl_Specimens UQ_Specimens_BoxPosition; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Specimens"
    ADD CONSTRAINT "UQ_Specimens_BoxPosition" UNIQUE ("BoxID", "PositionRow", "PositionCol");


--
-- Name: tbl_Studies UQ_Studies_StudyCode; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Studies"
    ADD CONSTRAINT "UQ_Studies_StudyCode" UNIQUE ("StudyCode");


--
-- Name: tbl_UserProfiles UQ_UserProfiles_AspNetUserId; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_UserProfiles"
    ADD CONSTRAINT "UQ_UserProfiles_AspNetUserId" UNIQUE ("AspNetUserId");


--
-- Name: EmailIndex; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "EmailIndex" ON public."AspNetUsers" USING btree ("NormalizedEmail");


--
-- Name: IX_AspNetRoleClaims_RoleId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AspNetRoleClaims_RoleId" ON public."AspNetRoleClaims" USING btree ("RoleId");


--
-- Name: IX_AspNetUserClaims_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AspNetUserClaims_UserId" ON public."AspNetUserClaims" USING btree ("UserId");


--
-- Name: IX_AspNetUserLogins_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AspNetUserLogins_UserId" ON public."AspNetUserLogins" USING btree ("UserId");


--
-- Name: IX_AspNetUserRoles_RoleId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AspNetUserRoles_RoleId" ON public."AspNetUserRoles" USING btree ("RoleId");


--
-- Name: IX_AuditLog_TableRecord; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AuditLog_TableRecord" ON public."tbl_AuditLog" USING btree ("TableName", "RecordID");


--
-- Name: IX_AuditLog_Timestamp; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AuditLog_Timestamp" ON public."tbl_AuditLog" USING btree ("Timestamp" DESC);


--
-- Name: RoleNameIndex; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "RoleNameIndex" ON public."AspNetRoles" USING btree ("NormalizedName") WHERE ("NormalizedName" IS NOT NULL);


--
-- Name: UserNameIndex; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UserNameIndex" ON public."AspNetUsers" USING btree ("NormalizedUserName") WHERE ("NormalizedUserName" IS NOT NULL);


--
-- Name: tbl_Approvals FK_Approvals_EDApprover; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Approvals"
    ADD CONSTRAINT "FK_Approvals_EDApprover" FOREIGN KEY ("EDApproverUserId") REFERENCES public."AspNetUsers"("Id") ON DELETE RESTRICT;


--
-- Name: tbl_Approvals FK_Approvals_PIApprover; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Approvals"
    ADD CONSTRAINT "FK_Approvals_PIApprover" FOREIGN KEY ("PIApproverUserId") REFERENCES public."AspNetUsers"("Id") ON DELETE RESTRICT;


--
-- Name: tbl_Approvals FK_Approvals_RegulatoryApprover; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Approvals"
    ADD CONSTRAINT "FK_Approvals_RegulatoryApprover" FOREIGN KEY ("RegulatoryApproverUserId") REFERENCES public."AspNetUsers"("Id") ON DELETE RESTRICT;


--
-- Name: tbl_Approvals FK_Approvals_RequestedBy; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Approvals"
    ADD CONSTRAINT "FK_Approvals_RequestedBy" FOREIGN KEY ("RequestedByUserId") REFERENCES public."AspNetUsers"("Id") ON DELETE RESTRICT;


--
-- Name: AspNetRoleClaims FK_AspNetRoleClaims_AspNetRoles_RoleId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetRoleClaims"
    ADD CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES public."AspNetRoles"("Id") ON DELETE CASCADE;


--
-- Name: AspNetUserClaims FK_AspNetUserClaims_AspNetUsers_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetUserClaims"
    ADD CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES public."AspNetUsers"("Id") ON DELETE CASCADE;


--
-- Name: AspNetUserLogins FK_AspNetUserLogins_AspNetUsers_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetUserLogins"
    ADD CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES public."AspNetUsers"("Id") ON DELETE CASCADE;


--
-- Name: AspNetUserRoles FK_AspNetUserRoles_AspNetRoles_RoleId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetUserRoles"
    ADD CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES public."AspNetRoles"("Id") ON DELETE CASCADE;


--
-- Name: AspNetUserRoles FK_AspNetUserRoles_AspNetUsers_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetUserRoles"
    ADD CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES public."AspNetUsers"("Id") ON DELETE CASCADE;


--
-- Name: AspNetUserTokens FK_AspNetUserTokens_AspNetUsers_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AspNetUserTokens"
    ADD CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES public."AspNetUsers"("Id") ON DELETE CASCADE;


--
-- Name: tbl_AuditLog FK_AuditLog_ChangedBy; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_AuditLog"
    ADD CONSTRAINT "FK_AuditLog_ChangedBy" FOREIGN KEY ("ChangedByUserId") REFERENCES public."AspNetUsers"("Id") ON DELETE RESTRICT;


--
-- Name: tbl_Boxes FK_Boxes_Racks; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Boxes"
    ADD CONSTRAINT "FK_Boxes_Racks" FOREIGN KEY ("RackID") REFERENCES public."tbl_Racks"("RackID") ON DELETE RESTRICT;


--
-- Name: tbl_FilterPaperUsages FK_FilterPaperUsages_ShipmentContent; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_FilterPaperUsages"
    ADD CONSTRAINT "FK_FilterPaperUsages_ShipmentContent" FOREIGN KEY ("ShipmentContentID") REFERENCES public."tbl_ShipmentContents"("ShipmentContentID") ON DELETE SET NULL;


--
-- Name: tbl_FilterPaperUsages FK_FilterPaperUsages_Specimens; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_FilterPaperUsages"
    ADD CONSTRAINT "FK_FilterPaperUsages_Specimens" FOREIGN KEY ("SpecimenID") REFERENCES public."tbl_Specimens"("SpecimenID") ON DELETE CASCADE;


--
-- Name: tbl_FilterPaperUsages FK_FilterPaperUsages_UsedBy; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_FilterPaperUsages"
    ADD CONSTRAINT "FK_FilterPaperUsages_UsedBy" FOREIGN KEY ("UsedByUserId") REFERENCES public."AspNetUsers"("Id") ON DELETE RESTRICT;


--
-- Name: tbl_Racks FK_Racks_Freezers; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Racks"
    ADD CONSTRAINT "FK_Racks_Freezers" FOREIGN KEY ("FreezerID") REFERENCES public."tbl_Freezers"("FreezerID") ON DELETE RESTRICT;


--
-- Name: tbl_ShipmentBatches FK_ShipmentBatches_Approval; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_ShipmentBatches"
    ADD CONSTRAINT "FK_ShipmentBatches_Approval" FOREIGN KEY ("ApprovalID") REFERENCES public."tbl_Approvals"("ApprovalID") ON DELETE RESTRICT;


--
-- Name: tbl_ShipmentBatches FK_ShipmentBatches_ImportedBy; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_ShipmentBatches"
    ADD CONSTRAINT "FK_ShipmentBatches_ImportedBy" FOREIGN KEY ("ImportedByUserId") REFERENCES public."AspNetUsers"("Id") ON DELETE RESTRICT;


--
-- Name: tbl_ShipmentContents FK_ShipmentContents_Shipment; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_ShipmentContents"
    ADD CONSTRAINT "FK_ShipmentContents_Shipment" FOREIGN KEY ("ShipmentID") REFERENCES public."tbl_Shipments"("ShipmentID") ON DELETE CASCADE;


--
-- Name: tbl_ShipmentContents FK_ShipmentContents_Specimen; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_ShipmentContents"
    ADD CONSTRAINT "FK_ShipmentContents_Specimen" FOREIGN KEY ("SpecimenID") REFERENCES public."tbl_Specimens"("SpecimenID") ON DELETE RESTRICT;


--
-- Name: tbl_ShipmentRequests FK_ShipmentRequests_Batch; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_ShipmentRequests"
    ADD CONSTRAINT "FK_ShipmentRequests_Batch" FOREIGN KEY ("BatchID") REFERENCES public."tbl_ShipmentBatches"("BatchID") ON DELETE RESTRICT;


--
-- Name: tbl_ShipmentRequests FK_ShipmentRequests_SampleType; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_ShipmentRequests"
    ADD CONSTRAINT "FK_ShipmentRequests_SampleType" FOREIGN KEY ("RequestedSampleTypeID") REFERENCES public."tbl_SampleTypes"("SampleTypeID") ON DELETE RESTRICT;


--
-- Name: tbl_ShipmentRequests FK_ShipmentRequests_Specimen; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_ShipmentRequests"
    ADD CONSTRAINT "FK_ShipmentRequests_Specimen" FOREIGN KEY ("MatchedSpecimenID") REFERENCES public."tbl_Specimens"("SpecimenID") ON DELETE RESTRICT;


--
-- Name: tbl_Shipments FK_Shipments_Batch; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Shipments"
    ADD CONSTRAINT "FK_Shipments_Batch" FOREIGN KEY ("BatchID") REFERENCES public."tbl_ShipmentBatches"("BatchID") ON DELETE RESTRICT;


--
-- Name: tbl_Shipments FK_Shipments_ShippedBox; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Shipments"
    ADD CONSTRAINT "FK_Shipments_ShippedBox" FOREIGN KEY ("ShippedBoxID") REFERENCES public."tbl_Boxes"("BoxID") ON DELETE RESTRICT;


--
-- Name: tbl_Shipments FK_Shipments_ShippedBy; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Shipments"
    ADD CONSTRAINT "FK_Shipments_ShippedBy" FOREIGN KEY ("ShippedByUserId") REFERENCES public."AspNetUsers"("Id") ON DELETE RESTRICT;


--
-- Name: tbl_Specimens FK_Specimens_Boxes; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Specimens"
    ADD CONSTRAINT "FK_Specimens_Boxes" FOREIGN KEY ("BoxID") REFERENCES public."tbl_Boxes"("BoxID");


--
-- Name: tbl_Specimens FK_Specimens_DiscardApproval; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Specimens"
    ADD CONSTRAINT "FK_Specimens_DiscardApproval" FOREIGN KEY ("DiscardApprovalID") REFERENCES public."tbl_Approvals"("ApprovalID") ON DELETE RESTRICT;


--
-- Name: tbl_Specimens FK_Specimens_SampleTypes; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Specimens"
    ADD CONSTRAINT "FK_Specimens_SampleTypes" FOREIGN KEY ("SampleTypeID") REFERENCES public."tbl_SampleTypes"("SampleTypeID");


--
-- Name: tbl_Specimens FK_Specimens_Studies; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_Specimens"
    ADD CONSTRAINT "FK_Specimens_Studies" FOREIGN KEY ("StudyID") REFERENCES public."tbl_Studies"("StudyID");


--
-- Name: tbl_UserProfiles FK_UserProfiles_AspNetUsers; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."tbl_UserProfiles"
    ADD CONSTRAINT "FK_UserProfiles_AspNetUsers" FOREIGN KEY ("AspNetUserId") REFERENCES public."AspNetUsers"("Id") ON DELETE CASCADE;


--
-- PostgreSQL database dump complete
--

\unrestrict x18mcdOchBy4Z81yyg8g9Ir0EwJAJUb9ovgZ6OSNCJahXZrYjL9YbLdTYQV8xpu

