namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

internal sealed partial class SqliteTreeRepository
{
    private const string SchemaInitializationSql = """
        CREATE TABLE IF NOT EXISTS item (
          id INTEGER PRIMARY KEY,
          uid TEXT NOT NULL UNIQUE,
          parent_id INTEGER REFERENCES item(id) ON DELETE CASCADE,

          kind INTEGER NOT NULL,

          title TEXT NOT NULL,
          position INTEGER NOT NULL,

          file_rel_path TEXT,

          child_count INTEGER NOT NULL DEFAULT 0,

          file_size INTEGER,
          file_mtime_utc INTEGER,
          content_hash TEXT,

          is_deleted INTEGER NOT NULL DEFAULT 0,

          created_at INTEGER NOT NULL,
          updated_at INTEGER NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_item_parent_position
        ON item(parent_id, position, id);

        CREATE INDEX IF NOT EXISTS idx_item_uid
        ON item(uid);

        CREATE INDEX IF NOT EXISTS idx_item_kind_title
        ON item(kind, title COLLATE NOCASE, id);

        CREATE INDEX IF NOT EXISTS idx_item_file_rel_path
        ON item(file_rel_path);

        CREATE TABLE IF NOT EXISTS document_mentions (
          id TEXT PRIMARY KEY,
          collection_id TEXT NOT NULL,
          source_document_id TEXT NOT NULL REFERENCES item(uid) ON DELETE CASCADE,
          source_block_id TEXT NOT NULL,
          target_document_id TEXT REFERENCES item(uid) ON DELETE SET NULL,
          mention_type TEXT NOT NULL,
          mention_text TEXT NOT NULL,
          normalized_mention_text TEXT NOT NULL,
          start_offset INTEGER NOT NULL,
          end_offset INTEGER NOT NULL,
          preview_text TEXT NOT NULL,
          context_before TEXT,
          context_after TEXT,
          status TEXT NOT NULL,
          created_at INTEGER NOT NULL,
          updated_at INTEGER NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_document_mentions_source
        ON document_mentions(collection_id, source_document_id, source_block_id, start_offset);

        CREATE INDEX IF NOT EXISTS idx_document_mentions_target
        ON document_mentions(collection_id, target_document_id, updated_at DESC);

        CREATE INDEX IF NOT EXISTS idx_document_mentions_text_status
        ON document_mentions(collection_id, normalized_mention_text, status);

        CREATE TABLE IF NOT EXISTS graph_view_settings (
          id INTEGER PRIMARY KEY CHECK (id = 1),
          neighbor_depth INTEGER NOT NULL DEFAULT 1,
          max_nodes INTEGER NOT NULL DEFAULT 75,
          updated_at INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS attachments (
          id TEXT PRIMARY KEY,
          collection_id TEXT NOT NULL,
          sha256 TEXT NOT NULL,
          original_file_name TEXT NOT NULL,
          stored_file_name TEXT NOT NULL,
          relative_path TEXT NOT NULL,
          content_type TEXT NOT NULL,
          file_extension TEXT NOT NULL,
          size_bytes INTEGER NOT NULL,
          created_at INTEGER NOT NULL,
          last_referenced_at INTEGER NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS idx_attachments_collection_sha256
        ON attachments(collection_id, sha256);

        CREATE INDEX IF NOT EXISTS idx_attachments_collection_type
        ON attachments(collection_id, content_type, last_referenced_at DESC);

        CREATE TABLE IF NOT EXISTS note_attachments (
          id TEXT PRIMARY KEY,
          collection_id TEXT NOT NULL,
          note_item_id INTEGER NOT NULL REFERENCES item(id) ON DELETE CASCADE,
          note_document_id TEXT NOT NULL,
          attachment_id TEXT NOT NULL REFERENCES attachments(id) ON DELETE CASCADE,
          display_name TEXT NOT NULL,
          created_at INTEGER NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS idx_note_attachments_note_attachment
        ON note_attachments(collection_id, note_item_id, attachment_id);

        CREATE INDEX IF NOT EXISTS idx_note_attachments_attachment
        ON note_attachments(collection_id, attachment_id, created_at DESC);

        CREATE TABLE IF NOT EXISTS tag (
          id INTEGER PRIMARY KEY,
          normalized_name TEXT NOT NULL UNIQUE,
          display_name TEXT NOT NULL,
          created_at INTEGER NOT NULL,
          updated_at INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS item_tag (
          item_id INTEGER NOT NULL REFERENCES item(id) ON DELETE CASCADE,
          tag_id INTEGER NOT NULL REFERENCES tag(id) ON DELETE CASCADE,
          created_at INTEGER NOT NULL,
          PRIMARY KEY (item_id, tag_id)
        );

        CREATE INDEX IF NOT EXISTS idx_tag_display_name
        ON tag(display_name COLLATE NOCASE, id);

        CREATE INDEX IF NOT EXISTS idx_item_tag_tag
        ON item_tag(tag_id, item_id);

        """;
}
