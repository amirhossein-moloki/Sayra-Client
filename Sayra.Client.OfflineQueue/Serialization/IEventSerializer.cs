namespace Sayra.Client.OfflineQueue.Serialization;

using Sayra.Client.OfflineQueue.Models;

public interface IEventSerializer
{
    string Serialize(ClientEvent clientEvent);
    ClientEvent Deserialize(string json);
    bool IsCompatible(string eventVersion);
}
