# Dockerfile dùng chung cho cả 3 service — chọn service qua build args PROJECT / APP_DLL.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG PROJECT
WORKDIR /src
COPY . .
RUN dotnet publish "$PROJECT" -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
ARG APP_DLL
WORKDIR /app
COPY --from=build /app .
ENV APP_DLL=${APP_DLL}
# .NET listen mặc định cổng 8080 trong container.
ENTRYPOINT ["sh", "-c", "dotnet $APP_DLL"]
