function shown(window)
    compositor.log('window shown!!!111')
end

function close(window)
    compositor.log('window closed')
end

compositor.log('Loading')
bind(compositor.WindowShown, shown)
bind(compositor.WindowLost, close)
compositor.log('Loaded!')
