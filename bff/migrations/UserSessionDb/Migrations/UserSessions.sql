CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;

CREATE TABLE "UserSessions" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_UserSessions" PRIMARY KEY AUTOINCREMENT,
    "ApplicationName" TEXT NULL,
    "SubjectId" TEXT NOT NULL,
    "SessionId" TEXT NULL,
    "Created" TEXT NOT NULL,
    "Renewed" TEXT NOT NULL,
    "Expires" TEXT NULL,
    "Ticket" TEXT NOT NULL,
    "Key" TEXT NOT NULL
);

CREATE UNIQUE INDEX "IX_UserSessions_ApplicationName_Key" ON "UserSessions" ("ApplicationName", "Key");

CREATE UNIQUE INDEX "IX_UserSessions_ApplicationName_SessionId" ON "UserSessions" ("ApplicationName", "SessionId");

CREATE UNIQUE INDEX "IX_UserSessions_ApplicationName_SubjectId_SessionId" ON "UserSessions" ("ApplicationName", "SubjectId", "SessionId");

CREATE INDEX "IX_UserSessions_Expires" ON "UserSessions" ("Expires");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20220314155751_UserSessions', '5.0.0');

COMMIT;

