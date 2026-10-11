import { createRoot } from 'react-dom/client';
import App from './App';
import '@fontsource/ibm-plex-sans-kr/400.css';
import '@fontsource/ibm-plex-sans-kr/600.css';
import '@fontsource/ibm-plex-sans-kr/700.css';
import '@fontsource/jetbrains-mono/400.css';
import './styles.css';
createRoot(document.getElementById('app')!).render(<App />);
