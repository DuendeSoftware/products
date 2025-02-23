#!/bin/bash

rm -rf "Data/Migrations"

dotnet ef migrations add Users -c ApplicationDbContext -o Data/Migrations
