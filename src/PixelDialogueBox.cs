using Godot;

public partial class PixelDialogueBox : Control
{
    [Export] public float BorderSize = 6.0f;
    [Export] public float CornerStep = 18.0f;

    public override void _Draw()
    {
        var size = Size;
        var border = BorderSize;
        var step = CornerStep;

        DrawRect(new Rect2(Vector2.Zero, size), Colors.Black);

        DrawRect(new Rect2(step, 0, size.X - step * 2.0f, border), Colors.White);
        DrawRect(new Rect2(step, size.Y - border, size.X - step * 2.0f, border), Colors.White);
        DrawRect(new Rect2(0, step, border, size.Y - step * 2.0f), Colors.White);
        DrawRect(new Rect2(size.X - border, step, border, size.Y - step * 2.0f), Colors.White);

        DrawRect(new Rect2(border, border, step, border), Colors.White);
        DrawRect(new Rect2(border, border, border, step), Colors.White);
        DrawRect(new Rect2(size.X - step - border, border, step, border), Colors.White);
        DrawRect(new Rect2(size.X - border * 2.0f, border, border, step), Colors.White);
        DrawRect(new Rect2(border, size.Y - border * 2.0f, step, border), Colors.White);
        DrawRect(new Rect2(border, size.Y - step - border, border, step), Colors.White);
        DrawRect(new Rect2(size.X - step - border, size.Y - border * 2.0f, step, border), Colors.White);
        DrawRect(new Rect2(size.X - border * 2.0f, size.Y - step - border, border, step), Colors.White);
    }
}
