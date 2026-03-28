run:
    dotnet run --project src/AiChat/AiChat.csproj

publish:
    dotnet publish src/AiChat/AiChat.csproj -c Release -o publish --self-contained true -p:PublishSingleFile=true

start:
    ./publish/AiChat

mcpjam:
    npx @mcpjam/inspector@latest 

ngrep:
    ngrep -d any -W byline -t '' tcp port 4713

tcp:
    tcpflow -aCg -i lo tcp port 4713