-- Copyright (c) Duende Software. All rights reserved.
-- See LICENSE in the project root for license information.

-- V001: Initial schema creation
-- Uses [[schemaname]] as a placeholder replaced at runtime.

DECLARE @compatLevel INT;
SELECT @compatLevel = CAST(compatibility_level AS INT) FROM sys.databases WHERE database_id = DB_ID();
IF @compatLevel < 140
BEGIN
    DECLARE @msg NVARCHAR(500) = CONCAT(
        'SQL Server database compatibility level ', @compatLevel,
        ' is not supported. A minimum compatibility level of 140 (SQL Server 2017) is required.');
    THROW 50001, @msg, 1;
END

-- Create schema if it doesn't exist (prerequisite for version check)
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'[[schemaname]]')
BEGIN
    EXEC('CREATE SCHEMA [[[schemaname]]]');
END

DECLARE @current_version INT = ISNULL((
    SELECT CAST(JSON_VALUE(CAST(value AS NVARCHAR(MAX)), '$.Version') AS INT)
    FROM sys.extended_properties ep
    WHERE ep.class = 3
      AND ep.name = N'SchemaVersion'
      AND ep.major_id = SCHEMA_ID('[[schemaname]]')
), 0);

IF @current_version < 1
BEGIN
    -- entities table
    CREATE TABLE [[[schemaname]]].[entities]
    (
        pool_id                 INT                 NOT NULL,
        entity_type_id           INT                 NOT NULL,
        entity_id                UNIQUEIDENTIFIER    NOT NULL,
        original_entity_id       UNIQUEIDENTIFIER    NOT NULL,
        entity_type_name         NVARCHAR(255)       NOT NULL,
        value                    NVARCHAR(MAX)       NOT NULL,
        dso_type_schema_version  INT                 NOT NULL,
        value_version            INT                 NOT NULL,
        created_at               DATETIMEOFFSET      NOT NULL    DEFAULT SYSDATETIMEOFFSET(),
        last_updated_at          DATETIMEOFFSET      NOT NULL    DEFAULT SYSDATETIMEOFFSET(),
        expires_at               DATETIMEOFFSET      NULL,
        CONSTRAINT PK_[[schemaname]]_entities PRIMARY KEY (pool_id, entity_type_id, entity_id)
    );

    CREATE INDEX IX_[[schemaname]]_entities_expires_at
        ON [[[schemaname]]].[entities] (expires_at)
        WHERE expires_at IS NOT NULL;

    CREATE INDEX IX_[[schemaname]]_entities_entity_type_name
        ON [[[schemaname]]].[entities] (entity_type_name);

    CREATE INDEX IX_[[schemaname]]_entities_created_at
        ON [[[schemaname]]].[entities] (pool_id, entity_type_id, created_at);

    CREATE INDEX IX_[[schemaname]]_entities_last_updated_at
        ON [[[schemaname]]].[entities] (pool_id, entity_type_id, last_updated_at);

    -- entity_keys table
    CREATE TABLE [[[schemaname]]].[entity_keys]
    (
        pool_id          INT                 NOT NULL,
        entity_type_id    INT                 NOT NULL,
        key_type_id       INT                 NOT NULL,
        entity_id         UNIQUEIDENTIFIER    NOT NULL,
        key_type_name     NVARCHAR(255)       NOT NULL,
        key_type_version  INT                 NOT NULL,
        key_value         UNIQUEIDENTIFIER    NOT NULL,
        key_json          NVARCHAR(MAX)       NULL,
        timestamp         DATETIMEOFFSET      NOT NULL    DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_[[schemaname]]_entity_keys PRIMARY KEY (pool_id, entity_type_id, key_type_id, key_type_version, key_value),
        CONSTRAINT FK_[[schemaname]]_entity_keys_entities FOREIGN KEY (pool_id, entity_type_id, entity_id)
            REFERENCES [[[schemaname]]].[entities] (pool_id, entity_type_id, entity_id)
            ON DELETE CASCADE
    );

    CREATE INDEX IX_[[schemaname]]_entity_keys_entity_type_id_entity_id
        ON [[[schemaname]]].[entity_keys] (entity_type_id, entity_id);

    -- search_values table
    CREATE TABLE [[[schemaname]]].[search_values]
    (
        pool_id          INT                 NOT NULL,
        entity_type_id    INT                 NOT NULL,
        entity_id         UNIQUEIDENTIFIER    NOT NULL,
        field_path        UNIQUEIDENTIFIER    NOT NULL,
        field_path_text   NVARCHAR(500)       NOT NULL,
        item_index        INT                 NOT NULL,
        string_value      NVARCHAR(500)       NULL,
        number_value      DECIMAL(38,18)      NULL,
        datetime_value    DATETIMEOFFSET      NULL,
        boolean_value     BIT                 NULL,
        guid_value        UNIQUEIDENTIFIER    NULL,
        CONSTRAINT PK_[[schemaname]]_search_values PRIMARY KEY (pool_id, entity_type_id, entity_id, field_path, item_index),
        CONSTRAINT FK_[[schemaname]]_search_values_entities FOREIGN KEY (pool_id, entity_type_id, entity_id)
            REFERENCES [[[schemaname]]].[entities] (pool_id, entity_type_id, entity_id)
            ON DELETE CASCADE
    );

    CREATE INDEX IX_[[schemaname]]_search_values_string_value
        ON [[[schemaname]]].[search_values] (pool_id, entity_type_id, field_path, string_value)
        WHERE string_value IS NOT NULL AND item_index = -1;

    CREATE INDEX IX_[[schemaname]]_search_values_number_value
        ON [[[schemaname]]].[search_values] (pool_id, entity_type_id, field_path, number_value)
        WHERE number_value IS NOT NULL AND item_index = -1;

    CREATE INDEX IX_[[schemaname]]_search_values_datetime_value
        ON [[[schemaname]]].[search_values] (pool_id, entity_type_id, field_path, datetime_value)
        WHERE datetime_value IS NOT NULL AND item_index = -1;

    CREATE INDEX IX_[[schemaname]]_search_values_boolean_value
        ON [[[schemaname]]].[search_values] (pool_id, entity_type_id, field_path, boolean_value)
        WHERE boolean_value IS NOT NULL AND item_index = -1;

    CREATE INDEX IX_[[schemaname]]_search_values_array_string_value
        ON [[[schemaname]]].[search_values] (pool_id, entity_type_id, entity_id, field_path, item_index, string_value)
        WHERE string_value IS NOT NULL AND item_index >= 0;

    CREATE INDEX IX_[[schemaname]]_search_values_array_number_value
        ON [[[schemaname]]].[search_values] (pool_id, entity_type_id, entity_id, field_path, item_index, number_value)
        WHERE number_value IS NOT NULL AND item_index >= 0;

    CREATE INDEX IX_[[schemaname]]_search_values_array_datetime_value
        ON [[[schemaname]]].[search_values] (pool_id, entity_type_id, entity_id, field_path, item_index, datetime_value)
        WHERE datetime_value IS NOT NULL AND item_index >= 0;

    CREATE INDEX IX_[[schemaname]]_search_values_array_boolean_value
        ON [[[schemaname]]].[search_values] (pool_id, entity_type_id, entity_id, field_path, item_index, boolean_value)
        WHERE boolean_value IS NOT NULL AND item_index >= 0;

    CREATE NONCLUSTERED INDEX [IX_[[schemaname]]_search_values_guid_value]
        ON [[[schemaname]]].[search_values] (pool_id, entity_type_id, field_path, guid_value)
        WHERE item_index = -1 AND guid_value IS NOT NULL;

    CREATE NONCLUSTERED INDEX [IX_[[schemaname]]_search_values_array_guid_value]
        ON [[[schemaname]]].[search_values] (pool_id, entity_type_id, entity_id, field_path, item_index, guid_value)
        WHERE item_index >= 0 AND guid_value IS NOT NULL;

    -- entity_links table
    CREATE TABLE [[[schemaname]]].[entity_links]
    (
        pool_id               INT               NOT NULL,
        link_type_id           INT               NOT NULL,
        left_entity_type_id    INT               NOT NULL,
        left_entity_id         UNIQUEIDENTIFIER  NOT NULL,
        right_entity_type_id   INT               NOT NULL,
        right_entity_id        UNIQUEIDENTIFIER  NOT NULL,
        created_at             DATETIMEOFFSET    NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_[[schemaname]]_entity_links PRIMARY KEY (pool_id, link_type_id, left_entity_id, right_entity_id)
    );

    CREATE INDEX IX_[[schemaname]]_entity_links_left_entity
        ON [[[schemaname]]].[entity_links] (pool_id, link_type_id, left_entity_id);

    CREATE INDEX IX_[[schemaname]]_entity_links_right_entity
        ON [[[schemaname]]].[entity_links] (pool_id, link_type_id, right_entity_id);

    CREATE INDEX IX_[[schemaname]]_entity_links_left_cascade
        ON [[[schemaname]]].[entity_links] (pool_id, left_entity_id);

    CREATE INDEX IX_[[schemaname]]_entity_links_right_cascade
        ON [[[schemaname]]].[entity_links] (pool_id, right_entity_id);

    -- outbox_subscriber_queue table
    CREATE TABLE [[[schemaname]]].[outbox_subscriber_queue]
    (
        sequence_number  BIGINT IDENTITY(1,1) NOT NULL,
        message_id       UNIQUEIDENTIFIER    NOT NULL,
        event_id         UNIQUEIDENTIFIER    NOT NULL,
        timestamp        DATETIMEOFFSET      NOT NULL,
        event_name       NVARCHAR(500)       NOT NULL,
        subject_id       UNIQUEIDENTIFIER    NOT NULL,
        entity_type_id   INT                 NOT NULL,
        entity_type_name NVARCHAR(255)       NOT NULL,
        pool_id         INT                 NOT NULL,
        payload          NVARCHAR(MAX)       NOT NULL,
        subscriber_name  NVARCHAR(255)       NOT NULL,
        CONSTRAINT PK_[[schemaname]]_outbox_subscriber_queue PRIMARY KEY CLUSTERED (sequence_number),
        CONSTRAINT UQ_[[schemaname]]_outbox_subscriber_queue_message_id UNIQUE (message_id)
    );

    CREATE INDEX IX_[[schemaname]]_outbox_subscriber_queue_subscriber
        ON [[[schemaname]]].[outbox_subscriber_queue] (subscriber_name, sequence_number);

    -- TVP types
    CREATE TYPE [[[schemaname]]].[KeyTableType] AS TABLE (
        key_type_id INT NOT NULL,
        key_type_name NVARCHAR(255) NOT NULL,
        key_type_version INT NOT NULL,
        key_value UNIQUEIDENTIFIER NOT NULL,
        key_json NVARCHAR(MAX) NULL
    );

    CREATE TYPE [[[schemaname]]].[SearchValueTableType] AS TABLE (
        field_path UNIQUEIDENTIFIER NOT NULL,
        field_path_text NVARCHAR(500) NOT NULL,
        item_index INT NOT NULL,
        string_value NVARCHAR(500) NULL,
        number_value DECIMAL(38,18) NULL,
        datetime_value DATETIMEOFFSET NULL,
        boolean_value BIT NULL,
        guid_value UNIQUEIDENTIFIER NULL
    );

    CREATE TYPE [[[schemaname]]].[EntityIdTableType] AS TABLE (
        entity_id UNIQUEIDENTIFIER NOT NULL
    );

    CREATE TYPE [[[schemaname]]].[ExpiredEntityKeyTableType] AS TABLE (
        pool_id       INT NOT NULL,
        entity_type_id INT NOT NULL,
        entity_id      UNIQUEIDENTIFIER NOT NULL
    );

    -- Version bump
    EXEC sys.sp_addextendedproperty
        @name = N'SchemaVersion',
        @value = N'{"Version":1}',
        @level0type = N'SCHEMA',
        @level0name = N'[[schemaname]]';
END
