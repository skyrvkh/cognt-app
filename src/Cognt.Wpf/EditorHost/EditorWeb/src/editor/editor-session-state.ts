import type { Doc } from '@blocksuite/store';

type EditorSessionStateOptions = {
  onTrackDoc?(doc: Doc): void;
  onUntrackDoc?(doc: Doc): void;
};

export class EditorSessionState {
  private currentDoc: Doc | null = null;
  private currentPath = '';
  private currentCollectionRoot = '';
  private loadingDocument = false;
  private loadGeneration = 0;
  private userEditGeneration = 0;

  public constructor(private readonly options: EditorSessionStateOptions = {}) {}

  public get doc() {
    return this.currentDoc;
  }

  public get path() {
    return this.currentPath;
  }

  public setPath(path: string) {
    this.currentPath = path;
  }

  public clearPath() {
    this.currentPath = '';
  }

  public get collectionRoot() {
    return this.currentCollectionRoot;
  }

  public setCollectionRoot(collectionRoot: string) {
    this.currentCollectionRoot = collectionRoot;
  }

  public get isLoading() {
    return this.loadingDocument;
  }

  public setLoading(loading: boolean) {
    this.loadingDocument = loading;
  }

  public get currentLoadGeneration() {
    return this.loadGeneration;
  }

  public nextLoadGeneration() {
    return ++this.loadGeneration;
  }

  public incrementUserEditGeneration() {
    return ++this.userEditGeneration;
  }

  public trackDoc(doc: Doc) {
    this.untrackDoc();
    this.currentDoc = doc;
    this.options.onTrackDoc?.(doc);
  }

  public untrackDoc() {
    if (this.currentDoc) {
      this.options.onUntrackDoc?.(this.currentDoc);
    }

    this.currentDoc = null;
  }

  public dispose() {
    this.untrackDoc();
    this.clearPath();
  }
}
