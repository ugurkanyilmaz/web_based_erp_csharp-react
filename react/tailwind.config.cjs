/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './index.html',
    './src/**/*.{js,jsx,ts,tsx,html}'
  ],
  theme: {
    extend: {},
  },
  plugins: [
    require('daisyui')
  ],
  // Optional: configure daisyUI themes
  daisyui: {
    themes: [
      'light'
    ]
  }
}
