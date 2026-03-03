public interface IResourceSave
{

    //TODO: add stopping token to pass through
    public Task<List<string?>> GetResources();
    public Task<bool> RemoveList(List<ImageShare> uris);
    public Task<bool> RemoveSingle(ImageShare resource);
    public Task<byte[]> GetResource(ImageShare resource, bool background = false);
    public Task<string> SaveResource(Guid resourceId, byte[] img, int screenWidth, int screenHeight, ImageShareSource source);
}