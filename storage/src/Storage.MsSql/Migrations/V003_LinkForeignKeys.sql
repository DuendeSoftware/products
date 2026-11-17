-- Copyright (c) Duende Software. All rights reserved.
-- See LICENSE in the project root for license information.

-- V003: Add foreign keys from entity_links to entities for both sides, and a
-- per-row restrict_delete column that marks links whose
-- LinkDefinition.OnRightDelete is Restrict. There is one link table: a
-- foreign key's ON DELETE action belongs to the constraint, not the row, so
-- both FKs are NO ACTION (this also sidesteps SQL Server error 1785, which
-- rejects a second ON DELETE CASCADE path from one child table to the same
-- parent table). The engine enforces Cascade and Restrict itself: before
-- deleting an entity, in the same transaction, it deletes entity_links rows
-- where the entity is the left side, and right-side rows that are not
-- restricted (restrict_delete = 0): a restricted right-side row that remains
-- makes the entity delete fail with a 547 foreign key violation, which the
-- engine reports as StillLinked.
--
-- Existing databases may hold links whose left or right endpoint doesn't
-- exist (no FKs before this migration), so invalid rows are deleted first:
-- otherwise ADD CONSTRAINT would fail. A link is invalid when no entity
-- exists with its (pool_id, entity_type_id, entity_id) on either side.
--
-- Uses [[schemaname]] as a placeholder replaced at runtime. Running this
-- script twice is safe: the cleanup delete is a no-op the second time (no
-- invalid rows remain), ADD COLUMN/ADD CONSTRAINT/CREATE INDEX/DROP INDEX
-- all use existence guards, and the version bump only applies once.

DECLARE @current_version INT = ISNULL((
    SELECT CAST(JSON_VALUE(CAST(value AS NVARCHAR(MAX)), '$.Version') AS INT)
    FROM sys.extended_properties ep
    WHERE ep.class = 3
      AND ep.name = N'SchemaVersion'
      AND ep.major_id = SCHEMA_ID('[[schemaname]]')
), 0);

IF @current_version < 3
BEGIN
    DELETE l
    FROM [[[schemaname]]].[entity_links] l
    WHERE NOT EXISTS (
            SELECT 1 FROM [[[schemaname]]].[entities] e
            WHERE e.pool_id = l.pool_id AND e.entity_type_id = l.left_entity_type_id AND e.entity_id = l.left_entity_id
          )
       OR NOT EXISTS (
            SELECT 1 FROM [[[schemaname]]].[entities] e
            WHERE e.pool_id = l.pool_id AND e.entity_type_id = l.right_entity_type_id AND e.entity_id = l.right_entity_id
          );

    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID('[[[schemaname]]].[entity_links]') AND name = 'restrict_delete'
    )
    BEGIN
        ALTER TABLE [[[schemaname]]].[entity_links]
            ADD restrict_delete BIT NOT NULL CONSTRAINT DF_[[schemaname]]_entity_links_restrict_delete DEFAULT 0;
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = 'FK_[[schemaname]]_entity_links_left'
          AND parent_object_id = OBJECT_ID('[[[schemaname]]].[entity_links]')
    )
    BEGIN
        ALTER TABLE [[[schemaname]]].[entity_links]
            ADD CONSTRAINT FK_[[schemaname]]_entity_links_left
                FOREIGN KEY (pool_id, left_entity_type_id, left_entity_id)
                REFERENCES [[[schemaname]]].[entities] (pool_id, entity_type_id, entity_id)
                ON DELETE NO ACTION;
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = 'FK_[[schemaname]]_entity_links_right'
          AND parent_object_id = OBJECT_ID('[[[schemaname]]].[entity_links]')
    )
    BEGIN
        ALTER TABLE [[[schemaname]]].[entity_links]
            ADD CONSTRAINT FK_[[schemaname]]_entity_links_right
                FOREIGN KEY (pool_id, right_entity_type_id, right_entity_id)
                REFERENCES [[[schemaname]]].[entities] (pool_id, entity_type_id, entity_id)
                ON DELETE NO ACTION;
    END

    -- Version bump
    EXEC sys.sp_updateextendedproperty
        @name = N'SchemaVersion',
        @value = N'{"Version":3}',
        @level0type = N'SCHEMA',
        @level0name = N'[[schemaname]]';
END

-- Indexes. IX_..._left_entity is
-- dropped: its columns are an exact prefix of the table's own primary key, so
-- it serves no access path the PK doesn't already cover. IX_..._right_entity
-- is kept (not a PK prefix): it still serves QueryLinksAsync and the
-- right-to-left link filter. The old IX_..._left_cascade/IX_..._right_cascade
-- (which only covered the FK's id column) are replaced with
-- IX_..._left_fk/IX_..._right_fk, whose leading columns match the FK columns
-- so the FK child lookup on parent delete is indexed: the right FK index also
-- carries link_type_id as a key column and restrict_delete/left_entity_id/
-- left_entity_type_id as INCLUDE columns, making it covering for the
-- right-to-left link filter, the restricted-link re-check on delete, and
-- QueryLinksAsync too.

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_[[schemaname]]_entity_links_left_entity'
           AND object_id = OBJECT_ID('[[[schemaname]]].[entity_links]'))
BEGIN
    DROP INDEX IX_[[schemaname]]_entity_links_left_entity ON [[[schemaname]]].[entity_links];
END

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_[[schemaname]]_entity_links_left_cascade'
           AND object_id = OBJECT_ID('[[[schemaname]]].[entity_links]'))
BEGIN
    DROP INDEX IX_[[schemaname]]_entity_links_left_cascade ON [[[schemaname]]].[entity_links];
END

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_[[schemaname]]_entity_links_right_cascade'
           AND object_id = OBJECT_ID('[[[schemaname]]].[entity_links]'))
BEGIN
    DROP INDEX IX_[[schemaname]]_entity_links_right_cascade ON [[[schemaname]]].[entity_links];
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_[[schemaname]]_entity_links_left_fk'
               AND object_id = OBJECT_ID('[[[schemaname]]].[entity_links]'))
BEGIN
    CREATE INDEX IX_[[schemaname]]_entity_links_left_fk
        ON [[[schemaname]]].[entity_links] (pool_id, left_entity_id, left_entity_type_id);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_[[schemaname]]_entity_links_right_fk'
               AND object_id = OBJECT_ID('[[[schemaname]]].[entity_links]'))
BEGIN
    CREATE INDEX IX_[[schemaname]]_entity_links_right_fk
        ON [[[schemaname]]].[entity_links] (pool_id, right_entity_id, right_entity_type_id, link_type_id)
        INCLUDE (restrict_delete, left_entity_id, left_entity_type_id);
END
