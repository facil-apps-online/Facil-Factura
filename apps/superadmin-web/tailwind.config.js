/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
    // Codigo compartido entre portales. En desarrollo vive en apps/_shared; en la imagen de
    // Docker el Dockerfile lo copia dentro de la app (./_shared). Se listan las dos rutas porque
    // el glob de Tailwind no falla si una no existe, simplemente no aporta archivos.
    "./_shared/**/*.{js,ts,jsx,tsx}",
    "../_shared/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {},
  },
  plugins: [require("tailwindcss-animate")],
}

