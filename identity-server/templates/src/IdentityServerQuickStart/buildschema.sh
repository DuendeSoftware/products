#!/bin/bash

rm -rf Migrations

dotnet ef migrations add Grants -c PersistedGrantDbContext -o Migrations/PersistedGrantDb
dotnet ef migrations add Configuration -c ConfigurationDbContext -o Migrations/ConfigurationDb

dotnet ef migrations script -c PersistedGrantDbContext -o Migrations/PersistedGrantDb.sql
dotnet ef migrations script -c ConfigurationDbContext -o Migrations/ConfigurationDb.sql
