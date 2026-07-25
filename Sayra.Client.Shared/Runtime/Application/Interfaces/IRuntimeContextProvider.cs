using System;
using Sayra.Client.Shared.Runtime.Domain.Entities;

namespace Sayra.Client.Shared.Runtime.Application.Interfaces
{
    public interface IRuntimeContextProvider
    {
        GameRuntimeContext GetContext();
        void SetContext(GameRuntimeContext context);
    }
}
