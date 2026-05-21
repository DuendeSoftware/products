-- V0 → V1: initial schema creation

CREATE TABLE IF NOT EXISTS __schema_info (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

-- Only run if current version < 1
-- SQLite lacks procedural IF, so we use IF NOT EXISTS on all objects
-- and the version bump at the end is guarded by a WHERE clause.

CREATE TABLE IF NOT EXISTS entities (
    pool_id                 INTEGER     NOT NULL,
    entity_type_id          INTEGER     NOT NULL,
    entity_id               TEXT        NOT NULL,
    entity_type_name        TEXT        NOT NULL,
    value                   TEXT        NOT NULL,
    dso_type_schema_version INTEGER     NOT NULL,
    value_version           INTEGER     NOT NULL,
    created_at              TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    last_updated_at         TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    expires_at              TEXT        NULL,
    PRIMARY KEY (pool_id, entity_type_id, entity_id)
);

CREATE INDEX IF NOT EXISTS entities_expires_at_index
ON entities (expires_at)
WHERE expires_at IS NOT NULL;

CREATE INDEX IF NOT EXISTS entities_created_at_index
ON entities (pool_id, entity_type_id, created_at);

CREATE INDEX IF NOT EXISTS entities_last_updated_at_index
ON entities (pool_id, entity_type_id, last_updated_at);

CREATE TABLE IF NOT EXISTS entity_keys (
    pool_id           INTEGER     NOT NULL,
    entity_type_id    INTEGER     NOT NULL,
    key_type_id       INTEGER     NOT NULL,
    key_type_version  INTEGER     NOT NULL,
    key_type_name     TEXT        NOT NULL,
    key_value         TEXT        NOT NULL,
    key_json          TEXT        NULL,
    entity_id         TEXT        NOT NULL,
    timestamp         TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    PRIMARY KEY (pool_id, entity_type_id, key_type_id, key_type_version, key_value),
    FOREIGN KEY (pool_id, entity_type_id, entity_id) REFERENCES entities (pool_id, entity_type_id, entity_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS entity_keys_entity_type_id_entity_id_index
ON entity_keys (entity_type_id, entity_id);

CREATE TABLE IF NOT EXISTS search_values (
    entity_type_id    INTEGER     NOT NULL,
    entity_id         TEXT        NOT NULL,
    field_path        BLOB        NOT NULL,
    field_path_text   TEXT        NOT NULL,
    item_index        INTEGER     NOT NULL,
    string_value      TEXT        NULL,
    number_value      REAL        NULL,
    datetime_value    TEXT        NULL,
    boolean_value     INTEGER     NULL,
    guid_value        TEXT        NULL,
    pool_id           INTEGER     NOT NULL,
    PRIMARY KEY (pool_id, entity_type_id, entity_id, field_path, item_index),
    FOREIGN KEY (pool_id, entity_type_id, entity_id) REFERENCES entities (pool_id, entity_type_id, entity_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS search_values_string_value_index
ON search_values (pool_id, entity_type_id, field_path, string_value)
WHERE string_value IS NOT NULL AND item_index = -1;

CREATE INDEX IF NOT EXISTS search_values_number_value_index
ON search_values (pool_id, entity_type_id, field_path, number_value)
WHERE number_value IS NOT NULL AND item_index = -1;

CREATE INDEX IF NOT EXISTS search_values_datetime_value_index
ON search_values (pool_id, entity_type_id, field_path, datetime_value)
WHERE datetime_value IS NOT NULL AND item_index = -1;

CREATE INDEX IF NOT EXISTS search_values_boolean_value_index
ON search_values (pool_id, entity_type_id, field_path, boolean_value)
WHERE boolean_value IS NOT NULL AND item_index = -1;

CREATE INDEX IF NOT EXISTS search_values_array_string_value_index
ON search_values (pool_id, entity_type_id, entity_id, field_path, item_index, string_value)
WHERE string_value IS NOT NULL AND item_index >= 0;

CREATE INDEX IF NOT EXISTS search_values_array_number_value_index
ON search_values (pool_id, entity_type_id, entity_id, field_path, item_index, number_value)
WHERE number_value IS NOT NULL AND item_index >= 0;

CREATE INDEX IF NOT EXISTS search_values_array_datetime_value_index
ON search_values (pool_id, entity_type_id, entity_id, field_path, item_index, datetime_value)
WHERE datetime_value IS NOT NULL AND item_index >= 0;

CREATE INDEX IF NOT EXISTS search_values_array_boolean_value_index
ON search_values (pool_id, entity_type_id, entity_id, field_path, item_index, boolean_value)
WHERE boolean_value IS NOT NULL AND item_index >= 0;

CREATE INDEX IF NOT EXISTS search_values_guid_value_index
ON search_values (pool_id, entity_type_id, field_path, guid_value)
WHERE item_index = -1 AND guid_value IS NOT NULL;

CREATE INDEX IF NOT EXISTS search_values_array_guid_value_index
ON search_values (pool_id, entity_type_id, entity_id, field_path, item_index, guid_value)
WHERE item_index >= 0 AND guid_value IS NOT NULL;

CREATE TABLE IF NOT EXISTS entity_links (
    pool_id                INTEGER     NOT NULL,
    link_type_id           INTEGER     NOT NULL,
    left_entity_type_id    INTEGER     NOT NULL,
    left_entity_id         TEXT        NOT NULL,
    right_entity_type_id   INTEGER     NOT NULL,
    right_entity_id        TEXT        NOT NULL,
    created_at             TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    PRIMARY KEY (pool_id, link_type_id, left_entity_id, right_entity_id)
);

CREATE INDEX IF NOT EXISTS entity_links_left_entity_index
ON entity_links (pool_id, link_type_id, left_entity_id);

CREATE INDEX IF NOT EXISTS entity_links_right_entity_index
ON entity_links (pool_id, link_type_id, right_entity_id);

CREATE INDEX IF NOT EXISTS entity_links_left_cascade_index
ON entity_links (pool_id, left_entity_id);

CREATE INDEX IF NOT EXISTS entity_links_right_cascade_index
ON entity_links (pool_id, right_entity_id);

CREATE TABLE IF NOT EXISTS outbox_subscriber_queue (
    sequence_number  INTEGER PRIMARY KEY AUTOINCREMENT,
    message_id       TEXT        NOT NULL,
    event_id         TEXT        NOT NULL,
    timestamp        TEXT        NOT NULL,
    event_name       TEXT        NOT NULL,
    subject_id       TEXT        NOT NULL,
    entity_type_id   INTEGER     NOT NULL,
    entity_type_name TEXT        NOT NULL,
    pool_id          INTEGER     NOT NULL,
    payload          TEXT        NOT NULL,
    subscriber_name  TEXT        NOT NULL,
    UNIQUE (message_id)
);

CREATE INDEX IF NOT EXISTS outbox_subscriber_queue_subscriber_index
ON outbox_subscriber_queue (subscriber_name, sequence_number);

-- Version bump: only update if not already at version 1
INSERT OR IGNORE INTO __schema_info (key, value) VALUES ('SchemaVersion', '{"Version":1}');
UPDATE __schema_info SET value = '{"Version":1}' WHERE key = 'SchemaVersion' AND json_extract(value, '$.Version') < 1;
