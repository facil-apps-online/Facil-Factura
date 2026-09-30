import fs from "fs"
import path from "path"
import { defineConfig } from "vite"
import react from "@vitejs/plugin-react"

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
      "react": path.resolve(__dirname, "./node_modules/react"),
      "react-dom": path.resolve(__dirname, "./node_modules/react-dom"),
      "lucide-react": path.resolve(__dirname, "./node_modules/lucide-react"),
      "@radix-ui/react-alert-dialog": path.resolve(__dirname, "./node_modules/@radix-ui/react-alert-dialog"),
      // Codigo compartido entre portales. En desarrollo vive en apps/_shared; en la imagen de
      // Docker el Dockerfile lo copia dentro de la app (./_shared) para que resuelva
      // node_modules. Se toma la que exista.
      "@shared": fs.existsSync(path.resolve(__dirname, "./_shared"))
        ? path.resolve(__dirname, "./_shared")
        : path.resolve(__dirname, "../_shared"),
    },
  },
})
