SELECT 'CREATE DATABASE journeys' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'journeys')\gexec
SELECT 'CREATE DATABASE rewards' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'rewards')\gexec
SELECT 'CREATE DATABASE notifications' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'notifications')\gexec

