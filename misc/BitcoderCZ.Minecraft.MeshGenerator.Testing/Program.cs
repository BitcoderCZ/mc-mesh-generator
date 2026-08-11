using BitcoderCZ.Maths.Vectors;
using BitcoderCZ.Minecraft.MeshGenerator;
using BitcoderCZ.Minecraft.MeshGenerator.Gltf;

#pragma warning disable IDE0059 // Unnecessary assignment of a value
Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

var manager = await ResourcePackManager.LoadAllAsync(new DirectoryInfo("rp"));

var chest = manager.GetModel("minecraft:item/light_gray_banner");
Console.WriteLine(chest.BuiltInInfo);

var bg = new WorldMeshGenerator(manager);
var model = await bg.GenerateFromZipFileAsync("/home/bitcoder/Downloads/[SHOP]_Lava_in_the_jungle_export.zip", int3.Zero);
var gltfConverter = new GltfConverter(manager);
var gltf = await gltfConverter.ConvertAsync(model);
gltf.SaveGLB("test.glb");
#pragma warning restore IDE0059 // Unnecessary assignment of a value

