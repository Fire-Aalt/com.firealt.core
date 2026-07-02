using BovineLabs.Essence;
using Unity.Entities;

namespace FireAlt.Core
{
    public static class ConsoleUtils
    {
        public static void GetWorld(out World world)
        {
            world = World.DefaultGameObjectInjectionWorld;
        }
        
        public static ref T GetSystemRef<T>(out World world) 
            where T : unmanaged, ISystem
        {
            GetWorld(out world);
            
            var systemHandle = world.GetExistingSystem<T>();
            ref var system = ref world.Unmanaged.GetUnsafeSystemRef<T>(systemHandle);
            return ref system;
        }
        
        public static T GetSystemManaged<T>(out World world) 
            where T : ComponentSystemBase
        {
            GetWorld(out world);
            
            var system = world.GetExistingSystemManaged<T>();
            return system;
        }

#if BL_ESSENSE
        public static IntrinsicWriter.Lookup GetIntrinsicLookup(ref SystemState state)
        {
            var lookup = new IntrinsicWriter.Lookup();
            var data = new IntrinsicWriter.SingletonData();
            lookup.Create(ref state);
            data.Create(ref state);
         
            lookup.Update(ref state, data);

            return lookup;
        }
#endif
    }
}