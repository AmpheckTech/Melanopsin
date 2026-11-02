using Melanopsin.Graphics;
using Melanopsin.Graphics.Three;
using Melanopsin.Mathematics;
using Melanopsin.UI;
using Melanopsin.UI.Markup;
using Melanopsin.UI.Three;

namespace Melanopsin.UI.Demo.Views;

[MarkupComponent]
public sealed partial class SceneSample : Component {
    public Scene ThreeScene { get; } = CreateScene();

    private static Scene CreateScene() {
        var scene = new Scene(new PerspectiveCamera(
            position: new Vector3(4.5f, 3.2f, 6),
            target: Vector3.Zero)) {
            ClearColor = new Color(14, 30, 27),
        };

        var cubeColors = new Color[] {
            new(45, 138, 111),
            new(34, 113, 91),
            new(68, 171, 136),
            new(28, 96, 78),
            new(57, 150, 120),
            new(24, 82, 68),
        };
        var cube = new SceneObject(Mesh.CreateBox(2.4f, 2.4f, 2.4f, cubeColors)) {
            Transform = Matrix4x4.CreateRotationY(0.45f) *
                Matrix4x4.CreateRotationX(-0.18f),
        };
        var floor = new SceneObject(Mesh.CreateBox(4.2f, 0.16f, 4.2f)) {
            Transform = Matrix4x4.CreateTranslation(new Vector3(0, -1.28f, 0)),
            Tint = new Color(54, 76, 67),
        };

        scene.Add(floor);
        scene.Add(cube);
        return scene;
    }
}
