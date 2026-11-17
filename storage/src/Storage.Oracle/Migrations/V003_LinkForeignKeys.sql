-- Copyright (c) Duende Software. All rights reserved.
-- See LICENSE in the project root for license information.

-- V003: Add foreign keys from ENTITY_LINKS to ENTITIES for both sides, and a
-- per-row RESTRICT_DELETE column that marks links whose
-- LinkDefinition.OnRightDelete is Restrict. There is one link table: a
-- foreign key's ON DELETE action belongs to the constraint, not the row, so
-- both FKs have no ON DELETE clause (Oracle has no RESTRICT keyword: the
-- default behaviour is NO ACTION). The engine enforces Cascade and Restrict
-- itself: before deleting an entity, in the same transaction, it deletes
-- ENTITY_LINKS rows where the entity is the left side, and right-side rows
-- that are not restricted (restrict_delete = 0): a restricted right-side row
-- that remains makes the entity delete fail with ORA-02292, which the engine
-- maps to StillLinked.
--
-- Existing databases may hold links whose left or right endpoint doesn't exist (no
-- FKs before this migration), so invalid rows are deleted first: otherwise ADD
-- CONSTRAINT would fail. A link is invalid when no entity exists with its
-- (pool_id, entity_type_id, entity_id) on either side.
--
-- [[schema]] is replaced at runtime with the quoted, schema-qualifying prefix.
--
-- Oracle DDL auto-commits, so a failure partway through this script leaves partial
-- state: every DDL statement below is wrapped in a PL/SQL block that ignores
-- "already exists"/"already used"/"does not exist" errors so a re-run finishes the
-- job. RAW(16) id columns are compared as raw values with no conversions.

DELETE FROM [[schema]]ENTITY_LINKS l
WHERE NOT EXISTS (
    SELECT 1 FROM [[schema]]ENTITIES e
    WHERE e.pool_id = l.pool_id
      AND e.entity_type_id = l.left_entity_type_id
      AND e.entity_id = l.left_entity_id
)
OR NOT EXISTS (
    SELECT 1 FROM [[schema]]ENTITIES e
    WHERE e.pool_id = l.pool_id
      AND e.entity_type_id = l.right_entity_type_id
      AND e.entity_id = l.right_entity_id
)
/

BEGIN
    EXECUTE IMMEDIATE q'[ALTER TABLE [[schema]]ENTITY_LINKS
        ADD RESTRICT_DELETE NUMBER(1) DEFAULT 0 NOT NULL]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE != -1430 THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[ALTER TABLE [[schema]]ENTITY_LINKS
        ADD CONSTRAINT FK_ENTITY_LINKS_LEFT FOREIGN KEY (pool_id, left_entity_type_id, left_entity_id)
            REFERENCES [[schema]]ENTITIES (pool_id, entity_type_id, entity_id)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-2264, -2275) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[ALTER TABLE [[schema]]ENTITY_LINKS
        ADD CONSTRAINT FK_ENTITY_LINKS_RIGHT FOREIGN KEY (pool_id, right_entity_type_id, right_entity_id)
            REFERENCES [[schema]]ENTITIES (pool_id, entity_type_id, entity_id)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-2264, -2275) THEN RAISE; END IF;
END;
/

-- Indexes. IX_ENTITY_LINKS_LEFT is
-- dropped: its columns are an exact prefix of the table's own primary key, so it
-- serves no access path the PK doesn't already cover. IX_ENTITY_LINKS_RIGHT is
-- kept (not a PK prefix). The old IX_ENTITY_LINKS_LEFT_CASCADE/
-- IX_ENTITY_LINKS_RIGHT_CASCADE (which only covered the FK's id column) are
-- replaced with IX_ENTITY_LINKS_LEFT_FK/IX_ENTITY_LINKS_RIGHT_FK, whose leading
-- columns match the FK columns so the FK child lookup on parent delete is
-- indexed (Oracle takes a table lock on the child table without a leading-column
-- index on the parent delete). Oracle has no INCLUDE/covering indexes, so the
-- trailing columns the right-to-left link filter, the restricted-link re-check
-- on delete, and QueryLinksAsync need are added as plain trailing key columns.

BEGIN
    EXECUTE IMMEDIATE q'[DROP INDEX [[schema]]IX_ENTITY_LINKS_LEFT]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE != -1418 THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[DROP INDEX [[schema]]IX_ENTITY_LINKS_LEFT_CASCADE]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE != -1418 THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[DROP INDEX [[schema]]IX_ENTITY_LINKS_RIGHT_CASCADE]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE != -1418 THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_ENTITY_LINKS_LEFT_FK ON [[schema]]ENTITY_LINKS (pool_id, left_entity_id, left_entity_type_id)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE q'[CREATE INDEX [[schema]]IX_ENTITY_LINKS_RIGHT_FK ON [[schema]]ENTITY_LINKS (pool_id, right_entity_id, right_entity_type_id, link_type_id, restrict_delete, left_entity_id, left_entity_type_id)]';
EXCEPTION WHEN OTHERS THEN IF SQLCODE NOT IN (-955, -1408) THEN RAISE; END IF;
END;
/

MERGE INTO [[schema]]"__SCHEMA_INFO" d
USING (SELECT 3 AS version FROM dual) s
ON (d.version = s.version)
WHEN NOT MATCHED THEN INSERT (version) VALUES (3)
/
