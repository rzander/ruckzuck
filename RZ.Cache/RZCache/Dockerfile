FROM microsoft/aspnetcore
ARG source
WORKDIR /app
EXPOSE 5000:5000/tcp
EXPOSE 5001:5001/udp
ENTRYPOINT ["dotnet", "RZWCF.dll"]
COPY ${source:-obj/Docker/publish} .
ENV localURL "https://rzproxy.azurewebsites.net"
ENV RZUser ""
ENV RZPW ""
ENV ParentServer "https://ruckzuck.azurewebsites.net/wcf/RZService.svc"
ENV CatalogTTL "4"