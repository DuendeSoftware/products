rmdir /S /Q Migrations

dotnet ef migrations add UserSessions -o Migrations/UserSessions
dotnet ef migrations script -o Migrations/UserSessions.sql
