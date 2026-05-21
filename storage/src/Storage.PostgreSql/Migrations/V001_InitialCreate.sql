
-- This statement has to be outside of the DO block to ensure the schema exists before we
-- attempt to read its comment for versioning
CREATE SCHEMA IF NOT EXISTS [[schemaname]];

-- Migration V0 → V1: initial schema creation
DO $$
DECLARE
    current_version INT;
BEGIN
    -- Read current version from schema comment
    SELECT COALESCE(
        (SELECT (obj_description('[[schemaname]]'::regnamespace)::jsonb->>'Version')::int
         WHERE obj_description('[[schemaname]]'::regnamespace) IS NOT NULL
           AND obj_description('[[schemaname]]'::regnamespace) NOT IN ('standard public schema', '')),
        0)
    INTO current_version;

    current_version := COALESCE(current_version, 0);

    IF current_version < 1 THEN

        CREATE SCHEMA IF NOT EXISTS [[schemaname]];

        CREATE TABLE [[schemaname]].entities
        (
            pool_id                 INTEGER                  NOT NULL,
            entity_type_id          INT                      NOT NULL,
            entity_id               UUID                     NOT NULL,
            entity_type_name        TEXT                     NOT NULL,
            value                   JSONB                    NOT NULL,
            dso_type_schema_version INT                      NOT NULL,
            value_version           INT                      NOT NULL,
            created_at              TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
            last_updated_at         TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
            expires_at              TIMESTAMP WITH TIME ZONE NULL,
            PRIMARY KEY (pool_id, entity_type_id, entity_id)
        );

        CREATE INDEX entities_expires_at_index
            ON [[schemaname]].entities (expires_at)
            WHERE expires_at IS NOT NULL;

        CREATE INDEX entities_created_at_index
            ON [[schemaname]].entities (pool_id, entity_type_id, created_at);

        CREATE INDEX entities_last_updated_at_index
            ON [[schemaname]].entities (pool_id, entity_type_id, last_updated_at);

        CREATE TABLE [[schemaname]].entity_keys
        (
            pool_id          INTEGER   NOT NULL,
            entity_type_id   INT       NOT NULL,
            key_type_id      INT       NOT NULL,
            key_type_version INT       NOT NULL,
            key_type_name    TEXT      NOT NULL,
            key_value        UUID      NOT NULL,
            key_json         JSONB     NULL,
            entity_id        UUID      NOT NULL,
            timestamp        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            PRIMARY KEY (pool_id, entity_type_id, key_type_id, key_type_version, key_value),
            FOREIGN KEY (pool_id, entity_type_id, entity_id)
                REFERENCES [[schemaname]].entities (pool_id, entity_type_id, entity_id) ON DELETE CASCADE
        );

        CREATE INDEX entity_keys_entity_type_id_entity_id_index
            ON [[schemaname]].entity_keys (entity_type_id, entity_id);

        CREATE TABLE [[schemaname]].search_values
        (
            entity_type_id  INT                      NOT NULL,
            entity_id       UUID                     NOT NULL,
            field_path      UUID                     NOT NULL,
            field_path_text TEXT                     NOT NULL,
            item_index      INT                      NOT NULL,
            string_value    TEXT                     NULL,
            number_value    NUMERIC                  NULL,
            datetime_value  TIMESTAMP WITH TIME ZONE NULL,
            boolean_value   BOOLEAN                  NULL,
            guid_value      UUID                     NULL,
            pool_id         INTEGER                  NOT NULL,
            PRIMARY KEY (pool_id, entity_type_id, entity_id, field_path, item_index),
            FOREIGN KEY (pool_id, entity_type_id, entity_id)
                REFERENCES [[schemaname]].entities (pool_id, entity_type_id, entity_id) ON DELETE CASCADE
        );

        CREATE INDEX search_values_string_value_index
            ON [[schemaname]].search_values (pool_id, entity_type_id, field_path, string_value)
            WHERE string_value IS NOT NULL AND item_index = -1;

        CREATE INDEX search_values_number_value_index
            ON [[schemaname]].search_values (pool_id, entity_type_id, field_path, number_value)
            WHERE number_value IS NOT NULL AND item_index = -1;

        CREATE INDEX search_values_datetime_value_index
            ON [[schemaname]].search_values (pool_id, entity_type_id, field_path, datetime_value)
            WHERE datetime_value IS NOT NULL AND item_index = -1;

        CREATE INDEX search_values_boolean_value_index
            ON [[schemaname]].search_values (pool_id, entity_type_id, field_path, boolean_value)
            WHERE boolean_value IS NOT NULL AND item_index = -1;

        CREATE INDEX search_values_array_string_value_index
            ON [[schemaname]].search_values (pool_id, entity_type_id, entity_id, field_path, item_index, string_value)
            WHERE string_value IS NOT NULL AND item_index >= 0;

        CREATE INDEX search_values_array_number_value_index
            ON [[schemaname]].search_values (pool_id, entity_type_id, entity_id, field_path, item_index, number_value)
            WHERE number_value IS NOT NULL AND item_index >= 0;

        CREATE INDEX search_values_array_datetime_value_index
            ON [[schemaname]].search_values (pool_id, entity_type_id, entity_id, field_path, item_index, datetime_value)
            WHERE datetime_value IS NOT NULL AND item_index >= 0;

        CREATE INDEX search_values_array_boolean_value_index
            ON [[schemaname]].search_values (pool_id, entity_type_id, entity_id, field_path, item_index, boolean_value)
            WHERE boolean_value IS NOT NULL AND item_index >= 0;

        CREATE INDEX search_values_guid_value_index
            ON [[schemaname]].search_values (pool_id, entity_type_id, field_path, guid_value)
            WHERE item_index = -1 AND guid_value IS NOT NULL;

        CREATE INDEX search_values_array_guid_value_index
            ON [[schemaname]].search_values (pool_id, entity_type_id, entity_id, field_path, item_index, guid_value)
            WHERE item_index >= 0 AND guid_value IS NOT NULL;

        CREATE TABLE [[schemaname]].entity_links
        (
            pool_id              INTEGER   NOT NULL,
            link_type_id         INT       NOT NULL,
            left_entity_type_id  INT       NOT NULL,
            left_entity_id       UUID      NOT NULL,
            right_entity_type_id INT       NOT NULL,
            right_entity_id      UUID      NOT NULL,
            created_at           TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            PRIMARY KEY (pool_id, link_type_id, left_entity_id, right_entity_id)
        );

        CREATE INDEX entity_links_left_entity_index
            ON [[schemaname]].entity_links (pool_id, link_type_id, left_entity_id);

        CREATE INDEX entity_links_right_entity_index
            ON [[schemaname]].entity_links (pool_id, link_type_id, right_entity_id);

        CREATE INDEX entity_links_left_cascade_index
            ON [[schemaname]].entity_links (pool_id, left_entity_id);

        CREATE INDEX entity_links_right_cascade_index
            ON [[schemaname]].entity_links (pool_id, right_entity_id);

        CREATE TABLE [[schemaname]].outbox_subscriber_queue
        (
            sequence_number  BIGINT GENERATED ALWAYS AS IDENTITY,
            message_id       UUID                     NOT NULL,
            event_id         UUID                     NOT NULL,
            timestamp        TIMESTAMP WITH TIME ZONE NOT NULL,
            event_name       TEXT                     NOT NULL,
            subject_id       UUID                     NOT NULL,
            entity_type_id   INT                      NOT NULL,
            entity_type_name TEXT                     NOT NULL,
            pool_id          INTEGER                  NOT NULL,
            payload          JSONB                    NOT NULL,
            subscriber_name  TEXT                     NOT NULL,
            PRIMARY KEY (sequence_number),
            UNIQUE (message_id)
        );

        CREATE INDEX outbox_subscriber_queue_subscriber_index
            ON [[schemaname]].outbox_subscriber_queue (subscriber_name, sequence_number);

        COMMENT ON SCHEMA [[schemaname]] IS '{"Version":1}';

    END IF;
END
$$;
