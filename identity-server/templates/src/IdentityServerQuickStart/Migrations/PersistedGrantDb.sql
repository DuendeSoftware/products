CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;

CREATE TABLE "DeviceCodes" (
    "UserCode" TEXT NOT NULL CONSTRAINT "PK_DeviceCodes" PRIMARY KEY,
    "DeviceCode" TEXT NOT NULL,
    "SubjectId" TEXT NULL,
    "SessionId" TEXT NULL,
    "ClientId" TEXT NOT NULL,
    "Description" TEXT NULL,
    "CreationTime" TEXT NOT NULL,
    "Expiration" TEXT NOT NULL,
    "Data" TEXT NOT NULL
);

CREATE TABLE "Keys" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Keys" PRIMARY KEY,
    "Version" INTEGER NOT NULL,
    "Created" TEXT NOT NULL,
    "Use" TEXT NULL,
    "Algorithm" TEXT NOT NULL,
    "IsX509Certificate" INTEGER NOT NULL,
    "DataProtected" INTEGER NOT NULL,
    "Data" TEXT NOT NULL
);

CREATE TABLE "PersistedGrants" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_PersistedGrants" PRIMARY KEY AUTOINCREMENT,
    "Key" TEXT NULL,
    "Type" TEXT NOT NULL,
    "SubjectId" TEXT NULL,
    "SessionId" TEXT NULL,
    "ClientId" TEXT NOT NULL,
    "Description" TEXT NULL,
    "CreationTime" TEXT NOT NULL,
    "Expiration" TEXT NULL,
    "ConsumedTime" TEXT NULL,
    "Data" TEXT NOT NULL
);

CREATE TABLE "PushedAuthorizationRequests" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_PushedAuthorizationRequests" PRIMARY KEY AUTOINCREMENT,
    "ReferenceValueHash" TEXT NOT NULL,
    "ExpiresAtUtc" TEXT NOT NULL,
    "Parameters" TEXT NOT NULL
);

CREATE TABLE "ServerSideSessions" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ServerSideSessions" PRIMARY KEY AUTOINCREMENT,
    "Key" TEXT NOT NULL,
    "Scheme" TEXT NOT NULL,
    "SubjectId" TEXT NOT NULL,
    "SessionId" TEXT NULL,
    "DisplayName" TEXT NULL,
    "Created" TEXT NOT NULL,
    "Renewed" TEXT NOT NULL,
    "Expires" TEXT NULL,
    "Data" TEXT NOT NULL
);

CREATE UNIQUE INDEX "IX_DeviceCodes_DeviceCode" ON "DeviceCodes" ("DeviceCode");

CREATE INDEX "IX_DeviceCodes_Expiration" ON "DeviceCodes" ("Expiration");

CREATE INDEX "IX_Keys_Use" ON "Keys" ("Use");

CREATE INDEX "IX_PersistedGrants_ConsumedTime" ON "PersistedGrants" ("ConsumedTime");

CREATE INDEX "IX_PersistedGrants_Expiration" ON "PersistedGrants" ("Expiration");

CREATE UNIQUE INDEX "IX_PersistedGrants_Key" ON "PersistedGrants" ("Key");

CREATE INDEX "IX_PersistedGrants_SubjectId_ClientId_Type" ON "PersistedGrants" ("SubjectId", "ClientId", "Type");

CREATE INDEX "IX_PersistedGrants_SubjectId_SessionId_Type" ON "PersistedGrants" ("SubjectId", "SessionId", "Type");

CREATE INDEX "IX_PushedAuthorizationRequests_ExpiresAtUtc" ON "PushedAuthorizationRequests" ("ExpiresAtUtc");

CREATE UNIQUE INDEX "IX_PushedAuthorizationRequests_ReferenceValueHash" ON "PushedAuthorizationRequests" ("ReferenceValueHash");

CREATE INDEX "IX_ServerSideSessions_DisplayName" ON "ServerSideSessions" ("DisplayName");

CREATE INDEX "IX_ServerSideSessions_Expires" ON "ServerSideSessions" ("Expires");

CREATE UNIQUE INDEX "IX_ServerSideSessions_Key" ON "ServerSideSessions" ("Key");

CREATE INDEX "IX_ServerSideSessions_SessionId" ON "ServerSideSessions" ("SessionId");

CREATE INDEX "IX_ServerSideSessions_SubjectId" ON "ServerSideSessions" ("SubjectId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20240123193240_Grants', '8.0.0');

COMMIT;

