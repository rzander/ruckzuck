FROM microsoft/aspnetcore
ARG source
WORKDIR /app
EXPOSE 5000:5000/tcp
EXPOSE 5001:5001/udp
EXPOSE 4001:4001
EXPOSE 4002:4002/udp
EXPOSE 5002:5002/tcp
EXPOSE 8080:8080
EXPOSE 8081:8081
ENTRYPOINT ["dotnet", "RZCache.dll"]
COPY ${source:-obj/Docker/publish} .
#RUN tar xvfz /app/wwwroot/go-ipfs_v0.4.13_linux-amd64.tar.gz
#RUN mv go-ipfs/ipfs /usr/local/bin/ipfs
#RUN rm /app/wwwroot/go-ipfs_v0.4.13_linux-amd64.tar.gz
ENV localURL "https://rzproxy.azurewebsites.net"
ENV RZUser ""
ENV RZPW ""
ENV ParentServer "https://ruckzuck.azurewebsites.net/wcf/RZService.svc"
ENV CatalogTTL "4"
ENV Proxy ""
ENV ProxyUserPW ""
ENV UseIPFS "0"
ENV WebPort "5000"
ENV UDPPort "5001"
ENV IPFS_PATH "/app/.ipfs"
ENV IPFS_LOGGING ""
#ENV IPFS_GW_URL "https://gateway.ipfs.io/ipfs"