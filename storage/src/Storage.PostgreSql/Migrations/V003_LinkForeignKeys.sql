-- Copyright (c) Duende Software. All rights reserved.
-- See LICENSE in the project root for license information.

-- V003: Add foreign keys from entity_links to entities for both sides, and a
-- per-row restrict_delete column that marks links whose
-- LinkDefinition.OnRightDelete is Restrict. There is one link table: a
-- foreign key's ON DELETE action belongs to the constraint, not the row, so
-- both FKs are NO ACTION. The application enforces Restrict by deleting
-- cascading right-side links before the entity delete and letting the FK
-- refuse the delete when a restricted link remains.
--
-- Existing databases may hold links whose left or right endpoint doesn't
-- exist (no FKs before this migration), so invalid rows are deleted first:
-- otherwise ADD CONSTRAINT would fail. A link is invalid when no entity
-- exists with its (pool_id, entity_type_id, entity_id) on either side.
--
-- [[schemaname]] is replaced at runtime with the schema name. Running this
-- script twice is safe: the cleanup delete is a no-op the second time (no
-- invalid rows remain), ADD COLUMN / ADD CONSTRAINT / CREATE INDEX all use
-- existence guards, and the version bump only applies once.

DO $$
BEGIN
    IF COALESCE(
        (SELECT (obj_description('[[schemaname]]'::regnamespace)::jsonb->>'Version')::int
         WHERE obj_description('[[schemaname]]'::regnamespace) IS NOT NULL
           AND obj_description('[[schemaname]]'::regnamespace) NOT IN ('standard public schema', '')),
        0) < 3 THEN

        DELETE FROM [[schemaname]].entity_links l
        WHERE NOT EXISTS (
                SELECT 1 FROM [[schemaname]].entities e
                WHERE e.pool_id = l.pool_id AND e.entity_type_id = l.left_entity_type_id AND e.entity_id = l.left_entity_id
              )
           OR NOT EXISTS (
                SELECT 1 FROM [[schemaname]].entities e
                WHERE e.pool_id = l.pool_id AND e.entity_type_id = l.right_entity_type_id AND e.entity_id = l.right_entity_id
              );

        IF NOT EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = '[[schemaname]]' AND table_name = 'entity_links' AND column_name = 'restrict_delete'
        ) THEN
            ALTER TABLE [[schemaname]].entity_links
                ADD COLUMN restrict_delete BOOLEAN NOT NULL DEFAULT FALSE;
        END IF;

        IF NOT EXISTS (
            SELECT 1 FROM pg_constraint
            WHERE conname = 'fk_entity_links_left'
              AND conrelid = '[[schemaname]].entity_links'::regclass
        ) THEN
            ALTER TABLE [[schemaname]].entity_links
                ADD CONSTRAINT fk_entity_links_left
                    FOREIGN KEY (pool_id, left_entity_type_id, left_entity_id)
                    REFERENCES [[schemaname]].entities (pool_id, entity_type_id, entity_id) ON DELETE NO ACTION;
        END IF;

        IF NOT EXISTS (
            SELECT 1 FROM pg_constraint
            WHERE conname = 'fk_entity_links_right'
              AND conrelid = '[[schemaname]].entity_links'::regclass
        ) THEN
            ALTER TABLE [[schemaname]].entity_links
                ADD CONSTRAINT fk_entity_links_right
                    FOREIGN KEY (pool_id, right_entity_type_id, right_entity_id)
                    REFERENCES [[schemaname]].entities (pool_id, entity_type_id, entity_id) ON DELETE NO ACTION;
        END IF;

        COMMENT ON SCHEMA [[schemaname]] IS '{"Version":3}';
    END IF;
END
$$;

-- Indexes. *_left_entity_index is dropped: its columns are an exact prefix of
-- the table's own primary key, so it serves no access path the PK doesn't
-- already cover. *_right_entity_index is kept (not a PK prefix): it still serves
-- QueryLinksAsync and the right-to-left link filter. The old
-- *_left_cascade_index/*_right_cascade_index (which only covered the FK's id
-- column) are replaced with *_left_fk_index/*_right_fk_index, whose leading
-- columns match the FK columns so the FK child lookup on parent delete is
-- indexed: the right FK index also carries link_type_id as a key column and
-- restrict_delete/left_entity_id/left_entity_type_id as INCLUDE columns,
-- making it covering for the right-to-left link filter, the restricted-link
-- re-check on delete, and QueryLinksAsync too.

DROP INDEX IF EXISTS [[schemaname]].entity_links_left_entity_index;
DROP INDEX IF EXISTS [[schemaname]].entity_links_left_cascade_index;
DROP INDEX IF EXISTS [[schemaname]].entity_links_right_cascade_index;

CREATE INDEX IF NOT EXISTS entity_links_right_entity_index
    ON [[schemaname]].entity_links (pool_id, link_type_id, right_entity_id);

CREATE INDEX IF NOT EXISTS entity_links_left_fk_index
    ON [[schemaname]].entity_links (pool_id, left_entity_id, left_entity_type_id);

CREATE INDEX IF NOT EXISTS entity_links_right_fk_index
    ON [[schemaname]].entity_links (pool_id, right_entity_id, right_entity_type_id, link_type_id)
    INCLUDE (restrict_delete, left_entity_id, left_entity_type_id);
