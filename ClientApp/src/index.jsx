import React, {Suspense} from 'react';
import ReactDOM from 'react-dom';
import { BrowserRouter } from 'react-router-dom';
import {App} from './App';
import './Utilities/i18next';
import './custom.css'
import {unregister} from "./registerServiceWorker";
import {Loading} from "./Components/Loading";
import {applyBrandTheme} from "./Theme";

// Register the brand theme before the first render so Fluent controls pick it
// up instead of falling back to the default Microsoft blue.
applyBrandTheme();

const baseUrl = document.getElementsByTagName('base')[0].getAttribute('href');
const rootElement = document.getElementById('root');

ReactDOM.render(
    <Suspense fallback={<Loading/>}>
      <BrowserRouter basename={baseUrl}>
        <App />
      </BrowserRouter>
    </Suspense>,
    rootElement);


unregister();