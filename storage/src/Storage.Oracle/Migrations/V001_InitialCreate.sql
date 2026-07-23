-- Copyright (c) Duende Software. All rights reserved.
-- See LICENSE in the project root for license information.

-- V001: Initial schema creation for the Oracle store.
--
-- [[schema]] is replaced at runtime with the quoted, schema-qualifying prefix
-- (for example "STORE". -> "STORE"."ENTITIES"), or an empty string for the
-- connecting user's own schema.
--
-- Statements are separated by a line containing a single slash because Oracle
-- executes one statement per command and cannot batch DDL.
--
-- Each DDL statement is wrapped in a PL/SQL block that ignores "already exists"
-- errors (ORA-00955 / ORA-01408) so the whole script is idempotent: it can be
-- run more than once (for example via BuildMigrationScript) without failing.
-- The DDL text uses q'[ ... ]' quoting so embedded characters need no escaping.
--
-- Oracle has no filtered (partial) indexes, so the MsSql "WHERE ... IS NOT NULL"
-- indexes are created as plain composite indexes here. GUIDs are stored as
-- RAW(16) in canonical big-endian order so the binary index sort is chronological
-- for UUIDv7 keys.

BEGIN
    EXECUTE IMMEDIATE q'[CREATE TABLE [[schema]]ENTITIES
    (
        pool_id                  NUMBER(10)                    NOT NULL,
        entity_type_id           NUMBER(10)                    NOT NULL,
        entity_id                RAW(16)                       NOT NULL,
        original_entity_id       RAW(16)                       NOT NULL,
        entity_type_name         NVARCHAR2(255)                NOT NULL,
        value                    CLOB                          NOT NULL,
        dso_type_schema_version  NUMBER(10)                    NOT NULL,
        value_version            NUMBER(10)                    NOT NULL,
        created_at               TIMESTAMP(6) WITH TIME ZONE   DEFAULT SYSTIMESTAMP NOT NULL,
        last_updated_at          TIMESTAMP(6) WITH TIME ZONE   DEFAULT SYSTIMESTAMP NOT NULL,
        expires_at               TIMESTAMP(6) WITH TIME ZONE,
        CONSTRAINT PK_ENTITIES PRIMARY KEY (pool_id, entity_type_id, entity_id)
    )]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_ENTITIES_EXPIRES_AT ON [[schema]]ENTITIES (expires_at)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_ENTITIES_ENTITY_TYPE_NAME ON [[schema]]ENTITIES (entity_type_name)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_ENTITIES_CREATED_AT ON [[schema]]ENTITIES (pool_id, entity_type_id, created_at)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_ENTITIES_LAST_UPDATED_AT ON [[schema]]ENTITIES (pool_id, entity_type_id, last_updated_at)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE TABLE [[schema]]ENTITY_KEYS
    (
        pool_id           NUMBER(10)                    NOT NULL,
        entity_type_id    NUMBER(10)                    NOT NULL,
        key_type_id       NUMBER(10)                    NOT NULL,
        entity_id         RAW(16)                       NOT NULL,
        key_type_name     NVARCHAR2(255)                NOT NULL,
        key_type_version  NUMBER(10)                    NOT NULL,
        key_value         RAW(16)                       NOT NULL,
        key_json          CLOB,
        timestamp         TIMESTAMP(6) WITH TIME ZONE   DEFAULT SYSTIMESTAMP NOT NULL,
        CONSTRAINT PK_ENTITY_KEYS PRIMARY KEY (pool_id, entity_type_id, key_type_id, key_type_version, key_value),
        CONSTRAINT FK_ENTITY_KEYS_ENTITIES FOREIGN KEY (pool_id, entity_type_id, entity_id)
            REFERENCES [[schema]]ENTITIES (pool_id, entity_type_id, entity_id)
            ON DELETE CASCADE
    )]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_ENTITY_KEYS_ENTITY ON [[schema]]ENTITY_KEYS (entity_type_id, entity_id)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE TABLE [[schema]]SEARCH_VALUES
    (
        pool_id           NUMBER(10)        NOT NULL,
        entity_type_id    NUMBER(10)        NOT NULL,
        entity_id         RAW(16)           NOT NULL,
        field_path        RAW(16)           NOT NULL,
        field_path_text   NVARCHAR2(500)    NOT NULL,
        item_index        NUMBER(10)        NOT NULL,
        string_value      NVARCHAR2(500),
        number_value      NUMBER(38,18),
        datetime_value    TIMESTAMP(6) WITH TIME ZONE,
        boolean_value     NUMBER(1),
        guid_value        RAW(16),
        CONSTRAINT PK_SEARCH_VALUES PRIMARY KEY (pool_id, entity_type_id, entity_id, field_path, item_index),
        CONSTRAINT CK_SEARCH_VALUES_BOOLEAN CHECK (boolean_value IS NULL OR boolean_value IN (0, 1)),
        CONSTRAINT FK_SEARCH_VALUES_ENTITIES FOREIGN KEY (pool_id, entity_type_id, entity_id)
            REFERENCES [[schema]]ENTITIES (pool_id, entity_type_id, entity_id)
            ON DELETE CASCADE
    )]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_SEARCH_VALUES_STRING ON [[schema]]SEARCH_VALUES (pool_id, entity_type_id, field_path, string_value)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_SEARCH_VALUES_NUMBER ON [[schema]]SEARCH_VALUES (pool_id, entity_type_id, field_path, number_value)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_SEARCH_VALUES_DATETIME ON [[schema]]SEARCH_VALUES (pool_id, entity_type_id, field_path, datetime_value)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_SEARCH_VALUES_BOOLEAN ON [[schema]]SEARCH_VALUES (pool_id, entity_type_id, field_path, boolean_value)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_SEARCH_VALUES_GUID ON [[schema]]SEARCH_VALUES (pool_id, entity_type_id, field_path, guid_value)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_SEARCH_VALUES_ARR_STRING ON [[schema]]SEARCH_VALUES (pool_id, entity_type_id, entity_id, field_path, item_index, string_value)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_SEARCH_VALUES_ARR_NUMBER ON [[schema]]SEARCH_VALUES (pool_id, entity_type_id, entity_id, field_path, item_index, number_value)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_SEARCH_VALUES_ARR_DATETIME ON [[schema]]SEARCH_VALUES (pool_id, entity_type_id, entity_id, field_path, item_index, datetime_value)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_SEARCH_VALUES_ARR_BOOLEAN ON [[schema]]SEARCH_VALUES (pool_id, entity_type_id, entity_id, field_path, item_index, boolean_value)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_SEARCH_VALUES_ARR_GUID ON [[schema]]SEARCH_VALUES (pool_id, entity_type_id, entity_id, field_path, item_index, guid_value)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE TABLE [[schema]]ENTITY_LINKS
    (
        pool_id               NUMBER(10)                    NOT NULL,
        link_type_id          NUMBER(10)                    NOT NULL,
        left_entity_type_id   NUMBER(10)                    NOT NULL,
        left_entity_id        RAW(16)                       NOT NULL,
        right_entity_type_id  NUMBER(10)                    NOT NULL,
        right_entity_id       RAW(16)                       NOT NULL,
        created_at            TIMESTAMP(6) WITH TIME ZONE   DEFAULT SYSTIMESTAMP NOT NULL,
        CONSTRAINT PK_ENTITY_LINKS PRIMARY KEY (pool_id, link_type_id, left_entity_id, right_entity_id)
    )]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_ENTITY_LINKS_LEFT ON [[schema]]ENTITY_LINKS (pool_id, link_type_id, left_entity_id)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_ENTITY_LINKS_RIGHT ON [[schema]]ENTITY_LINKS (pool_id, link_type_id, right_entity_id)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_ENTITY_LINKS_LEFT_CASCADE ON [[schema]]ENTITY_LINKS (pool_id, left_entity_id)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_ENTITY_LINKS_RIGHT_CASCADE ON [[schema]]ENTITY_LINKS (pool_id, right_entity_id)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE TABLE [[schema]]OUTBOX_SUBSCRIBER_QUEUE
    (
        sequence_number   NUMBER(19)        GENERATED ALWAYS AS IDENTITY,
        message_id        RAW(16)           NOT NULL,
        event_id          RAW(16)           NOT NULL,
        timestamp         TIMESTAMP(6) WITH TIME ZONE   NOT NULL,
        event_name        NVARCHAR2(500)    NOT NULL,
        subject_id        RAW(16)           NOT NULL,
        entity_type_id    NUMBER(10)        NOT NULL,
        entity_type_name  NVARCHAR2(255)    NOT NULL,
        pool_id           NUMBER(10)        NOT NULL,
        payload           CLOB              NOT NULL,
        subscriber_name   NVARCHAR2(255)    NOT NULL,
        CONSTRAINT PK_OUTBOX_SUBSCRIBER_QUEUE PRIMARY KEY (sequence_number),
        CONSTRAINT UQ_OUTBOX_MESSAGE_ID UNIQUE (message_id)
    )]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_OUTBOX_SUBSCRIBER ON [[schema]]OUTBOX_SUBSCRIBER_QUEUE (subscriber_name, sequence_number)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE TABLE [[schema]]"__SCHEMA_INFO"
    (
        version  NUMBER(10)   NOT NULL,
        CONSTRAINT PK_SCHEMA_INFO PRIMARY KEY (version)
    )]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

MERGE INTO [[schema]]"__SCHEMA_INFO" d
USING (SELECT 1 AS version FROM dual) s
ON (d.version = s.version)
WHEN NOT MATCHED THEN INSERT (version) VALUES (1)
/
