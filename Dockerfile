# =========================================================
# ÉTAPE 1 : RUNTIME (L'environnement d'exécution final)
# =========================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
# Port par défaut pour .NET 8+ / 9
EXPOSE 8080
# Note : Nginx pointera vers ce port 8080

# =========================================================
# ÉTAPE 2 : BUILD (Compilation et restauration NuGet)
# =========================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# On copie les fichiers .csproj individuellement pour optimiser le cache Docker.
# Si tu ne modifies pas tes dépendances, Docker sautera l'étape 'restore'.
COPY ["src/Symbolics.Com.Core.Api/Symbolics.Com.Core.Api.csproj", "src/Symbolics.Com.Core.Api/"]
COPY ["src/Symbolics.Com.Core.Contract/Symbolics.Com.Core.Contract.csproj", "src/Symbolics.Com.Core.Contract/"]
COPY ["src/Symbolics.Com.Core.Application/Symbolics.Com.Core.Application.csproj", "src/Symbolics.Com.Core.Application/"]
COPY ["src/Symbolics.Com.Core.Infrastructure/Symbolics.Com.Core.Infrastructure.csproj", "src/Symbolics.Com.Core.Infrastructure/"]

# Restauration des packages NuGet pour toute la solution
RUN dotnet restore "src/Symbolics.Com.Core.Api/Symbolics.Com.Core.Api.csproj"

# On copie maintenant tout le reste du code source
COPY . .

# Compilation du projet en mode Release
WORKDIR "/src/src/Symbolics.Com.Core.Api"
RUN dotnet build "Symbolics.Com.Core.Api.csproj" -c Release -o /app/build

# =========================================================
# ÉTAPE 3 : PUBLISH (Préparation des fichiers binaires)
# =========================================================
FROM build AS publish
RUN dotnet publish "Symbolics.Com.Core.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# =========================================================
# ÉTAPE 4 : FINAL (Assemblage de l'image de production)
# =========================================================
FROM base AS final
WORKDIR /app
# On récupère uniquement le résultat de la publication (DLLs compilées)
COPY --from=publish /app/publish .

# On définit un argument qui sera passé par GitHub Actions
ARG VERSION_ID=0.0.0
# On transforme cet argument en variable d'environnement persistante
ENV APP_VERSION=$VERSION_ID

# Commande de lancement de l'API
ENTRYPOINT ["dotnet", "Symbolics.Com.Core.Api.dll"]
