FROM microsoft/dotnet:2.1-aspnetcore-runtime AS base
WORKDIR /app
EXPOSE 80

FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /src
COPY . ./

RUN dotnet restore "PDFSharpMerge/PDFSharpMerge.csproj"
COPY . .
WORKDIR "/src"

RUN dotnet build "PDFSharpMerge/PDFSharpMerge.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "PDFSharpMerge/PDFSharpMerge.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "PDFSharpMerge.dll"]