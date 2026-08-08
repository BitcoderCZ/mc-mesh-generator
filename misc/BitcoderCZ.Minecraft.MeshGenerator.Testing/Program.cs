using BitcoderCZ.Minecraft.MeshGenerator;
using BitcoderCZ.Minecraft.MeshGenerator.Gltf;

#pragma warning disable IDE0059 // Unnecessary assignment of a value
Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

var manager = await ResourcePackManager.LoadAllAsync(new DirectoryInfo("rp"));

var chest = manager.GetModel("minecraft:item/bell");
Console.WriteLine(chest.BuiltInInfo);

var bg = new BlockMeshGenerator(manager);
var model = await bg.GenerateBlockModelAsync("minecraft:item/lily_of_the_valley");
var gltfConverter = new GltfConverter(manager);
var gltf = await gltfConverter.ConvertAsync(model);
gltf.SaveGLB("test.glb");
#pragma warning restore IDE0059 // Unnecessary assignment of a value

