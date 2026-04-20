using System.IO;
using Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Persistence;

namespace Cognt.Modules.PersonalKnowledgeManagement.Infrastructure.Notes;

internal sealed partial class NoteService
{
    private ITreeRepository OpenTreeRepositoryForCollectionId(string collectionId)
    {
        return OpenTreeRepositoryForCollectionPath(GetExistingCollectionRoot(collectionId));
    }

    private ITreeRepository OpenTreeRepositoryForCollectionPath(string collectionPath)
    {
        string root = NormalizeRoot(collectionPath);

        lock (repositoryLock)
        {
            ThrowIfDisposed();

            if (treeRepository is not null
                && string.Equals(treeRepositoryRoot, root, StringComparison.OrdinalIgnoreCase))
            {
                return treeRepository;
            }

            CloseTreeRepository();
            treeRepositoryRoot = root;
            treeRepository = new SqliteTreeRepository(Path.Combine(root, MetadataDatabaseFileName));
            MigrateLegacyFilesDirectory(root, treeRepository);

            if (!treeRepository.HasAnyItems())
            {
                BootstrapTreeMetadata(root, treeRepository);
            }

            return treeRepository;
        }
    }

    void IDisposable.Dispose()
    {
        lock (repositoryLock)
        {
            if (disposed)
            {
                return;
            }

            CloseTreeRepository();
            disposed = true;
        }
    }

    private void CloseTreeRepository()
    {
        treeRepository?.Dispose();
        treeRepository = null;
        treeRepositoryRoot = null;
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(NoteService));
        }
    }
}
