public interface IResourceSave
{
    public Task<bool> RemoveList(List<ImageShare> uris);
    public Task<bool> RemoveSingle(ImageShare resource);
    public Task<byte[]> GetResource(ImageShare resource, bool background = false);
    public Task<string> SaveResource(Guid resourceId, byte[] img, int screenWidth, int screenHeight, ImageShareSource source);
}