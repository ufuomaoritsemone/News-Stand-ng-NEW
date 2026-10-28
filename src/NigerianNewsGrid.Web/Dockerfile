# syntax=docker/dockerfile:1.7
FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN --mount=type=cache,id=npm,sharing=locked,target=/root/.npm \
    npm ci

COPY . ./
RUN npm run build

FROM node:22-alpine AS production-dependencies
WORKDIR /app
ENV NODE_ENV=production
COPY package.json package-lock.json ./
RUN --mount=type=cache,id=npm,sharing=locked,target=/root/.npm \
    npm ci --omit=dev

FROM node:22-alpine AS final
WORKDIR /app
ENV NODE_ENV=production \
    PORT=3000
COPY --from=production-dependencies --chown=node:node /app/node_modules ./node_modules
COPY --from=build --chown=node:node /app/package.json /app/package-lock.json ./
COPY --from=build --chown=node:node /app/build ./build
COPY --from=build --chown=node:node /app/public ./public

EXPOSE 3000
USER node
CMD ["npm", "run", "start"]
