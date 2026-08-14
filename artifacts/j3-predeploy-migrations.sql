-- Auditoria pré-deploy J3 (Passo 4.4D). Gerado por: dotnet ef migrations script
-- From: 20260802024320_AddMelhorEnvioQuoteSettings
-- To:   20260813221427_AddJ3FulfillmentAndResidentialAddress
-- Sem connection string, token ou senha.
-- NÃO executar contra Neon/produção neste passo.

START TRANSACTION;
ALTER TABLE "Orders" ALTER COLUMN "ShippingEstimatedDays" DROP NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260813214203_MakeShippingEstimatedDaysNullable', '9.0.4');

ALTER TABLE "Orders" ADD "ShippingIsResidentialAddress" boolean;

ALTER TABLE "Addresses" ADD "IsResidentialAddress" boolean;

CREATE TABLE "J3Fulfillments" (
    "Id" uuid NOT NULL,
    "OrderId" uuid NOT NULL,
    "Status" character varying(32) NOT NULL,
    "J3OrderId" character varying(64),
    "J3OrderCode" character varying(64),
    "J3TrackingNumber" character varying(64),
    "J3DeliveryPointId" character varying(64),
    "J3StampUrl" character varying(500),
    "AttemptCount" integer NOT NULL,
    "StartedAtUtc" timestamp with time zone,
    "CompletedAtUtc" timestamp with time zone,
    "LastErrorAtUtc" timestamp with time zone,
    "LastErrorCode" character varying(64),
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_J3Fulfillments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_J3Fulfillments_Orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES "Orders" ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_J3Fulfillments_J3OrderId" ON "J3Fulfillments" ("J3OrderId") WHERE "J3OrderId" IS NOT NULL;

CREATE UNIQUE INDEX "IX_J3Fulfillments_OrderId" ON "J3Fulfillments" ("OrderId");

CREATE INDEX "IX_J3Fulfillments_Status" ON "J3Fulfillments" ("Status");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260813221427_AddJ3FulfillmentAndResidentialAddress', '9.0.4');

COMMIT;
