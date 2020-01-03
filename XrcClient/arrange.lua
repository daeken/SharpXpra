function arrangeWindows()
    print('Arranging windows!')
    factor = 1.0 / 100
    circumference = 0
    for i, window in ipairs(compositor.TrueWindows) do
        circumference = circumference + window.BufferSize.Item1 * factor
    end
    radius = Mathf.Max(circumference / Mathf.PI / 2 * factor * 100, 3)
    i = 0
    step = Mathf.PI * 2 / circumference
    for j, window in ipairs(compositor.TrueWindows) do
        size = window.BufferSize.Item1 * factor * step / 2
        if j != 1 then
            i = i + size
        end
        window.Window.transform.position = Vector3.__new(-radius * Mathf.Sin(i), 0, radius * Mathf.Cos(i))
        window.Window.transform.rotation = Quaternion.AngleAxis(Mathf.Rad2Deg * i, Vector3.down)
        i = i + size
    end
    print('Arranged!')
end

function shown(window)
    arrangeWindows()
end

function close(window)
    arrangeWindows()
end

function resized(window)
    arrangeWindows()
end

print('Loading')
bind(compositor.WindowShown, shown)
bind(compositor.WindowLost, close)
arrangeWindows()
print('Loaded!')
