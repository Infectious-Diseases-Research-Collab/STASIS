#!/bin/bash

# Exit on any error
set -e

echo "🐘 Starting STASIS Database Setup..."

# 1. Create the user using your current Mac login as the admin
echo "👤 Setting up user 'stasis_app'..."
psql -U $(whoami) -d postgres -f 00_STASIS_create_db_user.sql

# 2. Create the database using your current Mac login as the admin
echo "🗄️ Creating database 'stasis'..."
psql -U $(whoami) -d postgres -c "CREATE DATABASE stasis OWNER stasis_app;" 2>/dev/null || echo "ℹ️ Database already exists, skipping."

# 3. Build tables
echo "🏗️ Building tables from 01_STASIS_create_tables_postgres.sql..."
psql -U stasis_app -d stasis -f 01_STASIS_create_tables_postgres.sql

# 4. Add fake data
# psql -U stasis_app -d stasis -f 02_STASIS_insert_fake_data.sql

echo "✅ Setup Complete!"