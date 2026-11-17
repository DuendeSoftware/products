-- Copyright (c) Duende Software. All rights reserved.
-- See LICENSE in the project root for license information.

-- V003: Add foreign keys from entity_links to entities for both sides, and a
-- per-row restrict_delete column that marks links whose
-- LinkDefinition.OnRightDelete is Restrict. There is one link table: a
-- foreign key's ON DELETE action belongs to the constraint, not the row, so
-- both FKs are NO ACTION and the application enforces Restrict by deleting
-- cascading right-side links before the entity delete and letting the FK
-- refuse the delete when a restricted link remains.
--
-- SQLite cannot add a foreign key to an existing table, so entity_links is
-- rebuilt: a new table with the FKs and the new column is created, only the
-- rows whose left and right endpoints both exist (as an entity of the
-- declared type, in the declared pool) are copied across, carrying each
-- row's restrict_delete value along (existing pre-V3 rows get false, see
-- below), the old table is dropped and the new one renamed into its place.
-- This is also the invalid-link cleanup the migration must do before adding
-- the constraints: a link row that never pointed at a real entity of the
-- declared type cannot satisfy the new FKs.
--
-- No PRAGMA statement is used here — this script runs inside the migration transaction on a
-- connection that already has `PRAGMA foreign_keys = ON` set (PRAGMA has no effect inside a
-- transaction in SQLite).
--
-- Running this script a second time against a V2 database that was already migrated to V3 is
-- not safe: like V002's own unguarded `ALTER TABLE ... ADD COLUMN`, SQLite has no "ADD COLUMN IF
-- NOT EXISTS", so the ALTER TABLE below fails with "duplicate column name" on a rerun, before any
-- other statement in this script runs and before any row is touched. Scripts are applied once per
-- version: MigrateAsync only runs a version's script when the stored version is behind it, so this
-- never happens through MigrateAsync itself — it can only happen if a caller re-applies the
-- exported migration SQL (see IStorageInstanceSchema.BuildMigrationScript) to a database that's
-- already at or past V3, which the "run migrations" how-to documents as not a safe thing to do.
--
-- The rebuild logic below is still written to carry restrict_delete through correctly if it ever
-- did run a second time (for example a future provider change that makes the ALTER TABLE
-- conditional): the INSERT copies the column's current value instead of a literal 0, so existing
-- V3 rows keep their real restrict_delete value rather than being reset to the V2->V3 default.

ALTER TABLE entity_links ADD COLUMN restrict_delete INTEGER NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS entity_links_v3 (
    pool_id                INTEGER     NOT NULL,
    link_type_id           INTEGER     NOT NULL,
    left_entity_type_id    INTEGER     NOT NULL,
    left_entity_id         TEXT        NOT NULL,
    right_entity_type_id   INTEGER     NOT NULL,
    right_entity_id        TEXT        NOT NULL,
    restrict_delete        INTEGER     NOT NULL DEFAULT 0,
    created_at             TEXT        NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    PRIMARY KEY (pool_id, link_type_id, left_entity_id, right_entity_id),
    FOREIGN KEY (pool_id, left_entity_type_id, left_entity_id) REFERENCES entities (pool_id, entity_type_id, entity_id) ON DELETE NO ACTION,
    FOREIGN KEY (pool_id, right_entity_type_id, right_entity_id) REFERENCES entities (pool_id, entity_type_id, entity_id) ON DELETE NO ACTION
);

-- Carries the column added above (or already present on a rerun) through the
-- rebuild unchanged: on the first (V2 -> V3) run every row predates the
-- column and reads back as 0 (the documented V3 default for pre-existing
-- rows) — on a rerun against an already-V3 database, every row's real
-- restrict_delete value (set by link writes, not by this migration) survives.
INSERT OR IGNORE INTO entity_links_v3 (pool_id, link_type_id, left_entity_type_id, left_entity_id, right_entity_type_id, right_entity_id, restrict_delete, created_at)
SELECT l.pool_id, l.link_type_id, l.left_entity_type_id, l.left_entity_id, l.right_entity_type_id, l.right_entity_id,
       l.restrict_delete, l.created_at
FROM entity_links l
WHERE EXISTS (
        SELECT 1 FROM entities e
        WHERE e.pool_id = l.pool_id AND e.entity_type_id = l.left_entity_type_id AND e.entity_id = l.left_entity_id
      )
  AND EXISTS (
        SELECT 1 FROM entities e
        WHERE e.pool_id = l.pool_id AND e.entity_type_id = l.right_entity_type_id AND e.entity_id = l.right_entity_id
      );

DROP TABLE entity_links;

ALTER TABLE entity_links_v3 RENAME TO entity_links;

-- Indexes. *_left_entity_index
-- is dropped: its columns (pool_id, link_type_id, left_entity_id) are an
-- exact prefix of the table's own primary key, so it serves no access path
-- the PK doesn't already cover. *_right_entity_index is kept: it is not a PK
-- prefix and still serves QueryLinksAsync and the right-to-left link filter.
-- The old *_left_cascade_index/*_right_cascade_index (which only covered the
-- FK's id column) are replaced with *_left_fk_index/*_right_fk_index, whose
-- leading columns match the FK columns so the FK child lookup on parent
-- delete is indexed. SQLite has no INCLUDE columns, so the right FK index
-- carries link_type_id, left_entity_id, left_entity_type_id and
-- restrict_delete as trailing key columns, making both the right-to-left
-- query and the restricted-link re-check on delete index-only.

DROP INDEX IF EXISTS entity_links_left_entity_index;

CREATE INDEX IF NOT EXISTS entity_links_right_entity_index
ON entity_links (pool_id, link_type_id, right_entity_id);

CREATE INDEX IF NOT EXISTS entity_links_left_fk_index
ON entity_links (pool_id, left_entity_id, left_entity_type_id);

CREATE INDEX IF NOT EXISTS entity_links_right_fk_index
ON entity_links (pool_id, right_entity_id, right_entity_type_id, link_type_id, restrict_delete, left_entity_id, left_entity_type_id);

-- Version bump: only update if not already at version 3
INSERT OR IGNORE INTO __schema_info (key, value) VALUES ('SchemaVersion', '{"Version":3}');
UPDATE __schema_info SET value = '{"Version":3}' WHERE key = 'SchemaVersion' AND json_extract(value, '$.Version') < 3;
