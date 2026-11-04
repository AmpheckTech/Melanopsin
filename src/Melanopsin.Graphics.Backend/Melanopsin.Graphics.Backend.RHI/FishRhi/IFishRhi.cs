namespace RHI.FishRhi;

public interface IFishRhi
{
    void Initialize(EGraphicsApiType graphicsApi);

    void Destroy();
}
