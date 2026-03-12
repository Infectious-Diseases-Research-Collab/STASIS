DO
$do$
BEGIN
   IF NOT EXISTS (
      SELECT FROM pg_catalog.pg_roles
      WHERE  rolname = 'stasis_app') THEN
      CREATE ROLE stasis_app WITH LOGIN PASSWORD 'your_password_here';
   END IF;
END
$do$;