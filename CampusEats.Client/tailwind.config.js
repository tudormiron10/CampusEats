/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./**/*.{razor,html,cshtml}",
    "./Pages/**/*.razor",
    "./Layout/**/*.razor",
    "./Shared/**/*.razor"
  ],
  theme: {
    extend: {
      colors: {
        'oxford-blue': {
          DEFAULT: '#002147',
          light: '#003366',
          dark: '#001530',
        },
        'tan': {
          DEFAULT: '#d2b48c',
          light: '#e5d4b8',
          dark: '#b89968',
        }
      },
      fontFamily: {
        sans: ['Segoe UI', 'Inter', '-apple-system', 'BlinkMacSystemFont', 'Roboto', 'Oxygen', 'sans-serif'],
      },
    },
  },
  plugins: [],
}