publish
```sh
dotnet publish UpsServer/UpsServer.csproj -c Release -r linux-musl-x64 --self-contained -p:PublishSingleFile=true -o out
```

---
alpine OpenRC
```sh
#!/sbin/openrc-run

description="UpsServer"
command="/app/UpsServer"
pidfile="/var/run/UpsServer.pid"
directory="/app"
supervisor=supervise-daemon
output_logger="logger -p user.info -t UpsServer"
error_logger="logger -p user.error -t UpsServer"
respawn_delay=2
respawn_max=10
respawn_period=60


depend() {
    need net
}

start_pre() {
    export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
    export Logging__LogLevel__Default=Warning
    export Logging__LogLevel__Microsoft=Information
    export Logging__Console__LogToStandardErrorThreshold=Error
    export Logging__Console__FormatterName=simple
    export Logging__Console__FormatterOptions__SingleLine=true
    export ha_token=
}
```