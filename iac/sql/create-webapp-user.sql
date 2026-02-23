IF NOT EXISTS (SELECT 1
FROM sys.database_principals
WHERE name = 'lightsaver-webapp-identity')
BEGIN
    CREATE USER [lightsaver-webapp-identity] FROM EXTERNAL PROVIDER;
END;

ALTER ROLE db_datareader ADD MEMBER [lightsaver-webapp-identity];
ALTER ROLE db_datawriter ADD MEMBER [lightsaver-webapp-identity];